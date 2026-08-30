using NSubstitute;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.Login;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Authentication;

/// <summary>
/// The login rules that actually carry security weight: BR-LOG-001 (one error for every
/// failure), LOG-004, LOG-006, LOG-012/LOG-013, BR-LOG-005 and BR-SET-005.
/// </summary>
public sealed class LoginWithEmailTests
{
    private readonly TestKit _kit = new();

    private LoginWithEmailCommandHandler CreateHandler() => new(
        _kit.Users,
        _kit.PasswordHasher,
        new LoginLockoutPolicy(_kit.LoginAttempts, _kit.Clock, _kit.AuthOptions),
        new LoginSessionIssuer(_kit.TokenService, _kit.Sessions, _kit.TokenGenerator, _kit.Clock, _kit.AuthOptions),
        _kit.EmailSender,
        _kit.Templates,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock);

    [Fact]
    public async Task Signs_in_a_verified_user_with_the_correct_password()
    {
        User user = TestKit.ActiveStudent();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.MustChangePassword);
        Assert.NotNull(result.Value.RefreshToken);
        Assert.Contains(nameof(RoleName.Student), result.Value.Roles);
    }

    /// <summary>
    /// BR-LOG-001. Asserting on the exact error object is the point: any divergence between
    /// these two paths turns the endpoint into an account-existence oracle.
    /// </summary>
    [Fact]
    public async Task Unknown_address_and_wrong_password_produce_the_identical_error()
    {
        User user = TestKit.ActiveStudent();

        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        Result<LoginResult> unknownAddress = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand("nobody@example.com", "any-password-at-all"),
            CancellationToken.None);

        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);

        Result<LoginResult> wrongPassword = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "wrong-password"),
            CancellationToken.None);

        Assert.True(unknownAddress.IsFailure);
        Assert.True(wrongPassword.IsFailure);
        Assert.Equal(unknownAddress.Error, wrongPassword.Error);
        Assert.Equal(AuthErrors.InvalidCredentials, wrongPassword.Error);
    }

    /// <summary>A Google-only account must not reveal itself as such through the login endpoint.</summary>
    [Fact]
    public async Task Google_only_account_reports_invalid_credentials_rather_than_its_provider()
    {
        User user = TestKit.GoogleOnlyUser();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "some-password-value"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    /// <summary>LOG-004. Distinct from invalid credentials, because the user can act on it.</summary>
    [Fact]
    public async Task Blocks_an_unverified_account_even_with_the_correct_password()
    {
        User user = TestKit.ActiveStudent(emailVerified: false);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailNotVerified, result.Error);
    }

    /// <summary>LOG-009: a deleted account is refused, and told nothing that distinguishes it.</summary>
    [Fact]
    public async Task Refuses_a_deleted_account()
    {
        User user = TestKit.ActiveStudent(status: UserStatus.Deleted);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    /// <summary>LOG-006 / BR-LOG-002: the threshold comes from configuration.</summary>
    [Fact]
    public async Task Locks_out_once_the_configured_threshold_is_reached()
    {
        _kit.Auth.MaxFailedLoginAttempts = 3;

        User user = TestKit.ActiveStudent();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);

        _kit.LoginAttempts
            .CountRecentFailuresForUserAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(3);

        _kit.LoginAttempts
            .GetLastFailureAtForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(TestKit.Now.AddMinutes(-1));

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.RateLimited, result.Error.Type);

        // The password was never even checked - the lockout short-circuits first.
        _kit.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Allows_sign_in_again_once_the_lockout_window_has_elapsed()
    {
        _kit.Auth.MaxFailedLoginAttempts = 3;
        _kit.Auth.LockoutMinutes = 15;

        User user = TestKit.ActiveStudent();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        _kit.LoginAttempts
            .CountRecentFailuresForUserAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(3);

        // Last failure is older than the lockout window, so the lock has expired.
        _kit.LoginAttempts
            .GetLastFailureAtForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(TestKit.Now.AddMinutes(-20));

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    /// <summary>LOG-012 / LOG-013: a forced reset yields a limited token and no refresh token.</summary>
    [Fact]
    public async Task Issues_a_limited_scope_token_when_a_password_change_is_required()
    {
        User user = TestKit.ActiveStudent(mustChangePassword: true);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.MustChangePassword);

        // No refresh token, and no roles on the token, so a route that forgot its policy still
        // has nothing to authorise against.
        Assert.Null(result.Value.RefreshToken);
        Assert.Empty(result.Value.Roles);

        await _kit.Sessions.DidNotReceive().AddAsync(Arg.Any<Domain.Authentication.Session>(), Arg.Any<CancellationToken>());
    }

    /// <summary>BR-LOG-005: Remember Me lengthens the refresh window, not the access token.</summary>
    [Fact]
    public async Task Remember_me_extends_only_the_refresh_token_lifetime()
    {
        _kit.Auth.RefreshTokenLifetimeDays = 7;
        _kit.Auth.RememberMeRefreshTokenLifetimeDays = 30;

        User user = TestKit.ActiveStudent();
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        Result<LoginResult> without = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password", RememberMe: false),
            CancellationToken.None);

        Result<LoginResult> with = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password", RememberMe: true),
            CancellationToken.None);

        Assert.Equal(TestKit.Now.AddDays(7), without.Value.RefreshTokenExpiresAt);
        Assert.Equal(TestKit.Now.AddDays(30), with.Value.RefreshTokenExpiresAt);

        // The access token expiry is identical in both cases.
        Assert.Equal(without.Value.ExpiresAt, with.Value.ExpiresAt);
    }

    /// <summary>
    /// Suspension extends LOG-009, but deliberately with a distinct message rather than the
    /// generic invalid-credentials error - the password is already proven at this point, so
    /// this cannot be used to enumerate accounts.
    /// </summary>
    [Fact]
    public async Task Refuses_a_suspended_account_with_a_distinct_message()
    {
        User user = TestKit.ActiveStudent(status: UserStatus.Suspended);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.AccountSuspended, result.Error);
        Assert.NotEqual(AuthErrors.InvalidCredentials, result.Error);
    }

    /// <summary>BR-SET-005: a correct sign-in brings a deactivated account back.</summary>
    [Fact]
    public async Task Reactivates_a_deactivated_account_on_a_successful_sign_in()
    {
        User user = TestKit.ActiveStudent(status: UserStatus.Deactivated);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _kit.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        Result<LoginResult> result = await CreateHandler().HandleAsync(
            new LoginWithEmailCommand(user.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        await _kit.Users.Received().UpdateAsync(user, Arg.Any<CancellationToken>());
    }
}
