using NSubstitute;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.Password;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Authentication;

/// <summary>FPW-003, FPW-004, FPW-005, FPW-007, FPW-009, FPW-010 / BR-FPW-002, BR-FPW-004.</summary>
public sealed class PasswordFlowTests
{
    private readonly TestKit _kit = new();

    private RequestPasswordResetCommandHandler CreateRequestHandler() => new(
        _kit.Users,
        _kit.ResetTokens,
        _kit.TokenGenerator,
        _kit.EmailSender,
        _kit.Templates,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock,
        _kit.AuthOptions,
        _kit.UrlOptions);

    private ResetPasswordCommandHandler CreateResetHandler() => new(
        _kit.ResetTokens,
        _kit.Users,
        _kit.PasswordHasher,
        _kit.Sessions,
        _kit.TokenGenerator,
        _kit.EmailSender,
        _kit.Templates,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock);

    /// <summary>
    /// FPW-003 / BR-FPW-002. Both cases must succeed identically; only the mail differs.
    /// </summary>
    [Fact]
    public async Task Reports_success_for_an_unknown_address_and_sends_nothing()
    {
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        Result result = await CreateRequestHandler().HandleAsync(
            new RequestPasswordResetCommand("nobody@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _kit.EmailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _kit.ResetTokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issues_a_reset_token_for_a_password_account()
    {
        User user = TestKit.ActiveStudent();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);

        Result result = await CreateRequestHandler().HandleAsync(
            new RequestPasswordResetCommand(user.Email.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Older links stop working the moment a new one is issued.
        await _kit.ResetTokens.Received().InvalidateAllForUserAsync(
            user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        await _kit.ResetTokens.Received().AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// FPW-009: a Google-only account gets a "sign in with Google" message and NO reset token,
    /// because there is no password for a token to reset.
    /// </summary>
    [Fact]
    public async Task Google_only_account_receives_guidance_but_no_reset_token()
    {
        User user = TestKit.GoogleOnlyUser();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);

        Result result = await CreateRequestHandler().HandleAsync(
            new RequestPasswordResetCommand(user.Email.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        _kit.Templates.Received().BuildPasswordResetForGoogleAccountEmail(
            user.Email.Value, user.FullName, Arg.Any<string>());

        await _kit.ResetTokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>FPW-005: a spent token cannot be replayed.</summary>
    [Fact]
    public async Task Rejects_an_already_used_reset_token()
    {
        User user = TestKit.ActiveStudent();

        var token = new PasswordResetToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddMinutes(30),
            TestKit.Now.AddMinutes(-5), usedAt: TestKit.Now.AddMinutes(-1));

        _kit.ResetTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);

        Result result = await CreateResetHandler().HandleAsync(
            new ResetPasswordCommand("tok", "brand-new-password-value"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UsedResetToken, result.Error);
    }

    /// <summary>FPW-004.</summary>
    [Fact]
    public async Task Rejects_an_expired_reset_token()
    {
        User user = TestKit.ActiveStudent();

        var token = new PasswordResetToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddMinutes(-1),
            TestKit.Now.AddHours(-2));

        _kit.ResetTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);

        Result result = await CreateResetHandler().HandleAsync(
            new ResetPasswordCommand("tok", "brand-new-password-value"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.ExpiredResetToken, result.Error);
    }

    /// <summary>FPW-007: reusing the existing password defeats the point of the reset.</summary>
    [Fact]
    public async Task Rejects_a_new_password_matching_the_current_one()
    {
        User user = TestKit.ActiveStudent();

        var token = new PasswordResetToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddMinutes(30), TestKit.Now);

        _kit.ResetTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("same-password-again", user.PasswordHash).Returns(true);

        Result result = await CreateResetHandler().HandleAsync(
            new ResetPasswordCommand("tok", "same-password-again"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PasswordMatchesCurrent, result.Error);
    }

    /// <summary>FPW-010 / BR-FPW-004: every session dies, including any an attacker holds.</summary>
    [Fact]
    public async Task Successful_reset_revokes_every_session()
    {
        User user = TestKit.ActiveStudent();

        var token = new PasswordResetToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddMinutes(30), TestKit.Now);

        _kit.ResetTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        _kit.PasswordHasher.Hash("brand-new-password-value").Returns("new-hash");

        Result result = await CreateResetHandler().HandleAsync(
            new ResetPasswordCommand("tok", "brand-new-password-value"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-hash", user.PasswordHash);

        await _kit.Sessions.Received(1).RevokeAllForUserAsync(
            user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        await _kit.ResetTokens.Received(1).MarkUsedAsync(
            token.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Clearing must_change_password is what ends the forced-reset state (LOG-014).</summary>
    [Fact]
    public async Task Reset_clears_the_forced_password_change_flag()
    {
        User user = TestKit.ActiveStudent(mustChangePassword: true);

        var token = new PasswordResetToken(
            Guid.CreateVersion7(), user.Id, "hash:tok", TestKit.Now.AddMinutes(30), TestKit.Now);

        _kit.ResetTokens.GetByHashAsync("hash:tok", Arg.Any<CancellationToken>()).Returns(token);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        _kit.PasswordHasher.Hash(Arg.Any<string>()).Returns("new-hash");

        await CreateResetHandler().HandleAsync(
            new ResetPasswordCommand("tok", "brand-new-password-value"),
            CancellationToken.None);

        Assert.False(user.MustChangePassword);
    }
}
