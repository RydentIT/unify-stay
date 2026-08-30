using NSubstitute;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.EmailVerification;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Authentication;

/// <summary>EVR-001, EVR-005 / BR-EVR-002, and the three distinct outcomes of EVR-010.</summary>
public sealed class EmailVerificationTests
{
    private readonly TestKit _kit = new();

    private SendVerificationEmailCommandHandler CreateSendHandler() => new(
        _kit.Users,
        _kit.VerificationTokens,
        _kit.TokenGenerator,
        _kit.EmailSender,
        _kit.Templates,
        _kit.AuditLogger,
        _kit.Clock,
        _kit.AuthOptions,
        _kit.UrlOptions);

    private VerifyEmailCommandHandler CreateVerifyHandler() => new(
        _kit.VerificationTokens,
        _kit.Users,
        _kit.TokenGenerator,
        _kit.AuditLogger,
        _kit.Clock);

    /// <summary>BR-EVR-002: issuing a new token retires any outstanding one first.</summary>
    [Fact]
    public async Task Issuing_a_verification_token_invalidates_the_previous_one()
    {
        User user = TestKit.ActiveStudent(emailVerified: false);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Result result = await CreateSendHandler().HandleAsync(
            new SendVerificationEmailCommand(user.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        Received.InOrder(() =>
        {
            _kit.VerificationTokens.InvalidateAllForUserAsync(
                user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

            _kit.VerificationTokens.AddAsync(
                Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// Admin/Staff accounts have no session to land in on the user portal, so their link must
    /// point at the admin portal instead - the only one of the two portals ever reachable for
    /// them (relevant to bootstrap-admin, whose account starts unverified).
    /// </summary>
    [Fact]
    public async Task Sends_admin_accounts_to_the_admin_portal_for_verification()
    {
        User admin = TestKit.ActiveStudent(emailVerified: false);
        admin.AddRole(RoleName.Admin);
        _kit.Users.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        await CreateSendHandler().HandleAsync(new SendVerificationEmailCommand(admin.Id), CancellationToken.None);

        _kit.Templates.Received().BuildVerificationEmail(
            admin.Email.Value,
            admin.FullName,
            Arg.Is<string>(url => url.StartsWith(_kit.Urls.AdminPortalBaseUrl, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Sends_student_accounts_to_the_user_portal_for_verification()
    {
        User student = TestKit.ActiveStudent(emailVerified: false);
        _kit.Users.GetByIdAsync(student.Id, Arg.Any<CancellationToken>()).Returns(student);

        await CreateSendHandler().HandleAsync(new SendVerificationEmailCommand(student.Id), CancellationToken.None);

        _kit.Templates.Received().BuildVerificationEmail(
            student.Email.Value,
            student.FullName,
            Arg.Is<string>(url => url.StartsWith(_kit.Urls.UserPortalBaseUrl, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Refuses_to_send_when_the_address_is_already_verified()
    {
        User user = TestKit.ActiveStudent(emailVerified: true);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Result result = await CreateSendHandler().HandleAsync(
            new SendVerificationEmailCommand(user.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.AlreadyVerified, result.Error);
    }

    [Fact]
    public async Task Verifies_the_account_with_a_valid_token()
    {
        User user = TestKit.ActiveStudent(emailVerified: false);

        var token = new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddHours(12), TestKit.Now);

        _kit.VerificationTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Result result = await CreateVerifyHandler().HandleAsync(
            new VerifyEmailCommand("tok"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.EmailVerified);

        await _kit.VerificationTokens.Received(1).MarkUsedAsync(
            token.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// EVR-010. These three cases are asserted separately because each needs a different next
    /// action from the user; collapsing them into one message would help nobody.
    /// </summary>
    [Fact]
    public async Task Unrecognised_expired_and_used_tokens_report_distinct_errors()
    {
        User user = TestKit.ActiveStudent(emailVerified: false);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Unrecognised.
        _kit.VerificationTokens.GetByHashAsync("hash:missing", Arg.Any<CancellationToken>())
            .Returns((EmailVerificationToken?)null);

        Result unrecognised = await CreateVerifyHandler()
            .HandleAsync(new VerifyEmailCommand("missing"), CancellationToken.None);

        // Expired.
        _kit.VerificationTokens.GetByHashAsync("hash:expired", Arg.Any<CancellationToken>())
            .Returns(new EmailVerificationToken(
                Guid.CreateVersion7(), user.Id, "hash:expired", TestKit.Now.AddMinutes(-1), TestKit.Now.AddDays(-2)));

        Result expired = await CreateVerifyHandler()
            .HandleAsync(new VerifyEmailCommand("expired"), CancellationToken.None);

        // Already used.
        _kit.VerificationTokens.GetByHashAsync("hash:used", Arg.Any<CancellationToken>())
            .Returns(new EmailVerificationToken(
                Guid.CreateVersion7(), user.Id, "hash:used", TestKit.Now.AddHours(6), TestKit.Now,
                usedAt: TestKit.Now.AddMinutes(-5)));

        Result used = await CreateVerifyHandler()
            .HandleAsync(new VerifyEmailCommand("used"), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidVerificationToken, unrecognised.Error);
        Assert.Equal(AuthErrors.ExpiredVerificationToken, expired.Error);
        Assert.Equal(AuthErrors.UsedVerificationToken, used.Error);

        // All three genuinely differ.
        Assert.Equal(3, new HashSet<string> { unrecognised.Error.Code, expired.Error.Code, used.Error.Code }.Count);
    }

    /// <summary>An old link must not resurrect a deleted account (domain guard).</summary>
    [Fact]
    public async Task Refuses_to_verify_a_deleted_account()
    {
        User user = TestKit.ActiveStudent(emailVerified: false, status: UserStatus.Deleted);

        var token = new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddHours(12), TestKit.Now);

        _kit.VerificationTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Result result = await CreateVerifyHandler().HandleAsync(
            new VerifyEmailCommand("tok"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(user.EmailVerified);
    }

    /// <summary>The resend endpoint must not confirm whether an address has an account.</summary>
    [Fact]
    public async Task Resend_reports_success_for_an_unknown_address()
    {
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new ResendVerificationEmailCommandHandler(
            _kit.Users, _kit.VerificationTokens, _kit.Dispatcher, _kit.Clock);

        Result result = await handler.HandleAsync(
            new ResendVerificationEmailCommand("nobody@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _kit.Dispatcher.DidNotReceive()
            .SendAsync(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>());
    }

    /// <summary>EVR-006: the per-account throttle stops inbox flooding.</summary>
    [Fact]
    public async Task Resend_stops_after_the_per_account_limit()
    {
        User user = TestKit.ActiveStudent(emailVerified: false);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);

        _kit.VerificationTokens
            .CountIssuedSinceAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var handler = new ResendVerificationEmailCommandHandler(
            _kit.Users, _kit.VerificationTokens, _kit.Dispatcher, _kit.Clock);

        Result result = await handler.HandleAsync(
            new ResendVerificationEmailCommand(user.Email.Value),
            CancellationToken.None);

        // Reports success regardless, but nothing was actually dispatched.
        Assert.True(result.IsSuccess);
        await _kit.Dispatcher.DidNotReceive()
            .SendAsync(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>());
    }
}
