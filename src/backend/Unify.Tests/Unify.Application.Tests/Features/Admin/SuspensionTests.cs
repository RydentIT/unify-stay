using NSubstitute;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Common;
using Unify.Application.Features.Admin;
using Unify.Application.Features.Authentication.Login;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Admin;

/// <summary>
/// Part 4: suspend (ban) and lift-suspension. "Suspended" = "banned" - there is no appeal or
/// login-through path here, deliberately (that is a later, separate module).
/// </summary>
public sealed class SuspensionTests
{
    private readonly TestKit _kit = new();
    private readonly User _admin = TestKit.ActiveStudent(email: "admin@example.com");

    public SuspensionTests() => _kit.SignIn(_admin);

    private SuspendUserCommandHandler CreateSuspendHandler() => new(
        _kit.Users,
        _kit.Suspensions,
        _kit.Sessions,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock);

    private LiftSuspensionCommandHandler CreateLiftHandler() => new(
        _kit.Users,
        _kit.Suspensions,
        _kit.EmailSender,
        _kit.Templates,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock);

    [Fact]
    public void Reason_is_required_to_suspend_an_account()
    {
        var validator = new SuspendUserCommandValidator();

        var result = validator.Validate(new SuspendUserCommand(Guid.CreateVersion7(), string.Empty));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Suspends_an_active_user_and_revokes_their_sessions()
    {
        User target = TestKit.ActiveStudent();
        _kit.Users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        Result result = await CreateSuspendHandler().HandleAsync(
            new SuspendUserCommand(target.Id, "Repeated policy violations"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Suspended, target.Status);

        await _kit.Users.Received(1).UpdateAsync(target, Arg.Any<CancellationToken>());
        await _kit.Suspensions.Received(1).AddAsync(Arg.Any<UserSuspension>(), Arg.Any<CancellationToken>());
        await _kit.Sessions.Received(1).RevokeAllForUserAsync(target.Id, TestKit.Now, Arg.Any<CancellationToken>());

        await _kit.AuditLogger.Received(1).LogAsync(
            Arg.Is<AuditEvent>(e => e.ActionType == AuditActions.UserSuspended && e.UserId == target.Id),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.Deactivated)]
    [InlineData(UserStatus.Deleted)]
    public async Task Refuses_to_suspend_an_account_that_is_not_eligible(UserStatus status)
    {
        User target = TestKit.ActiveStudent(status: status);
        _kit.Users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        Result result = await CreateSuspendHandler().HandleAsync(
            new SuspendUserCommand(target.Id, "Some reason"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            status == UserStatus.Suspended ? AuthErrors.UserAlreadySuspended : AuthErrors.UserNotEligibleForSuspension,
            result.Error);

        await _kit.Sessions.DidNotReceive().RevokeAllForUserAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lift_fails_when_there_is_no_active_suspension()
    {
        User target = TestKit.ActiveStudent(status: UserStatus.Suspended);
        _kit.Users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _kit.Suspensions.GetActiveForUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns((UserSuspension?)null);

        Result result = await CreateLiftHandler().HandleAsync(
            new LiftSuspensionCommand(target.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.NoActiveSuspension, result.Error);
    }

    [Fact]
    public async Task Lift_restores_active_status_notifies_the_user_and_logs_the_action()
    {
        User target = TestKit.ActiveStudent(status: UserStatus.Suspended);
        _kit.Users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        UserSuspension suspension = UserSuspension.Impose(
            Guid.CreateVersion7(), target.Id, "Repeated policy violations", _admin.Id, TestKit.Now.AddDays(-1));
        _kit.Suspensions.GetActiveForUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns(suspension);

        Result result = await CreateLiftHandler().HandleAsync(
            new LiftSuspensionCommand(target.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, target.Status);
        Assert.False(suspension.IsActive);

        await _kit.Users.Received(1).UpdateAsync(target, Arg.Any<CancellationToken>());
        await _kit.Suspensions.Received(1).UpdateAsync(suspension, Arg.Any<CancellationToken>());

        await _kit.AuditLogger.Received(1).LogAsync(
            Arg.Is<AuditEvent>(e => e.ActionType == AuditActions.SuspensionLifted && e.UserId == target.Id),
            Arg.Any<CancellationToken>());

        await _kit.EmailSender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// End-to-end across the two handlers that matter here: lifting the suspension is what makes
    /// the exact same account loggable-in again, with no separate "reactivate" step required.
    /// </summary>
    [Fact]
    public async Task Login_succeeds_again_once_the_suspension_is_lifted()
    {
        User target = TestKit.ActiveStudent(status: UserStatus.Suspended);
        _kit.Users.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        UserSuspension suspension = UserSuspension.Impose(
            Guid.CreateVersion7(), target.Id, "Repeated policy violations", _admin.Id, TestKit.Now.AddDays(-1));
        _kit.Suspensions.GetActiveForUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns(suspension);

        Result liftResult = await CreateLiftHandler().HandleAsync(
            new LiftSuspensionCommand(target.Id), CancellationToken.None);
        Assert.True(liftResult.IsSuccess);

        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(target);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);

        var loginHandler = new LoginWithEmailCommandHandler(
            _kit.Users,
            _kit.PasswordHasher,
            new LoginLockoutPolicy(_kit.LoginAttempts, _kit.Clock, _kit.AuthOptions),
            new LoginSessionIssuer(_kit.TokenService, _kit.Sessions, _kit.TokenGenerator, _kit.Clock, _kit.AuthOptions),
            _kit.EmailSender,
            _kit.Templates,
            _kit.AuditLogger,
            _kit.CurrentUser,
            _kit.Clock);

        Result<LoginResult> loginResult = await loginHandler.HandleAsync(
            new LoginWithEmailCommand(target.Email.Value, "correct-password"),
            CancellationToken.None);

        Assert.True(loginResult.IsSuccess);
    }
}
