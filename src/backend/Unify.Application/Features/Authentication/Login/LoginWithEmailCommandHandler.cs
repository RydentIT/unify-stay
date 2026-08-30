using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// Email + password sign-in.
///
/// The controlling rule is BR-LOG-001: unknown address, wrong password, Google-only account and
/// wrong provider all return the SAME error. The only outcomes that differ are the ones the
/// user must act on differently - unverified email (LOG-004), a lockout (LOG-006), a deleted
/// account, and the forced password change (LOG-012).
///
/// Deactivated accounts are the deliberate exception to "reject inactive accounts": a correct
/// password reactivates them (BR-SET-005), which is why the status check happens after
/// verification rather than before.
/// </summary>
internal sealed class LoginWithEmailCommandHandler : ICommandHandler<LoginWithEmailCommand, Result<LoginResult>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly LoginLockoutPolicy _lockout;
    private readonly LoginSessionIssuer _sessionIssuer;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public LoginWithEmailCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        LoginLockoutPolicy lockout,
        LoginSessionIssuer sessionIssuer,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _lockout = lockout;
        _sessionIssuer = sessionIssuer;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<LoginResult>> HandleAsync(
        LoginWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        string? ip = _currentUser.IpAddress;

        if (!Email.TryCreate(request.Email, out Email? parsed) || parsed is null)
        {
            // Malformed address: still a failed attempt as far as throttling is concerned.
            await _lockout.RecordFailureAsync(null, request.Email, ip, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        Email email = parsed;
        User? user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        LockoutState lockout = await _lockout
            .EvaluateAsync(user, email.Value, ip, cancellationToken)
            .ConfigureAwait(false);

        if (lockout.IsLockedOut)
        {
            await LogAsync(AuditActions.LoginLocked, user?.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<LoginResult>(
                AuthErrors.AccountLocked(lockout.LockedUntil ?? _lockout.LockedUntilFrom(_clock.UtcNow)));
        }

        // Unknown address, or an account with no password (Google-only). Both answer identically
        // to a wrong password - saying "this account uses Google" here would confirm the address
        // exists, which is exactly what BR-LOG-001 forbids.
        if (user is null || !user.HasPassword)
        {
            await RecordFailedAttemptAsync(user, email.Value, ip, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        if (user.IsDeleted)
        {
            await LogAsync(AuditActions.LoginBlockedInactive, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await RecordFailedAttemptAsync(user, email.Value, ip, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // Suspension (ban) extends LOG-009. The password is already proven at this point, so
        // revealing a distinct message cannot be used to enumerate accounts - it is a deliberate
        // exception to BR-LOG-001, same reasoning as LOG-004 below. The reason itself is never
        // exposed here, only that the account is suspended.
        if (user.Status == UserStatus.Suspended)
        {
            await LogAsync(AuditActions.LoginBlockedSuspended, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<LoginResult>(AuthErrors.AccountSuspended);
        }

        // LOG-004: correct password, but the address was never confirmed. Distinct from invalid
        // credentials because the user has a concrete action to take.
        if (!user.EmailVerified)
        {
            await LogAsync(AuditActions.LoginBlockedUnverified, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<LoginResult>(AuthErrors.EmailNotVerified);
        }

        DateTimeOffset now = _clock.UtcNow;

        // BR-SET-005: a successful sign-in is how a deactivated account comes back.
        if (user.Status == UserStatus.Deactivated)
        {
            user.Reactivate(now);
            await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

            await LogAsync(AuditActions.AccountReactivated, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        await _lockout.RecordSuccessAsync(user.Id, email.Value, ip, cancellationToken).ConfigureAwait(false);

        LoginResult result = await _sessionIssuer
            .IssueAsync(user, request.RememberMe, cancellationToken)
            .ConfigureAwait(false);

        await LogAsync(
            result.MustChangePassword ? AuditActions.LoginPasswordChangeRequired : AuditActions.LoginSucceeded,
            user.Id,
            email.Value,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(result);
    }

    /// <summary>Records the failure and, if this is the one that trips the threshold, warns the owner.</summary>
    private async Task RecordFailedAttemptAsync(
        User? user,
        string email,
        string? ip,
        CancellationToken cancellationToken)
    {
        bool nowLocked = await _lockout
            .RecordFailureAsync(user?.Id, email, ip, cancellationToken)
            .ConfigureAwait(false);

        await LogAsync(AuditActions.LoginFailed, user?.Id, email, cancellationToken).ConfigureAwait(false);

        if (nowLocked && user is not null)
        {
            await _emailSender.SendAsync(
                _templates.BuildAccountLockedEmail(
                    user.Email.Value,
                    user.FullName,
                    _lockout.LockedUntilFrom(_clock.UtcNow)),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private Task LogAsync(string action, Guid? userId, string email, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = action,
                UserId = userId,
                Email = email,
                Provider = nameof(AuthProviderKind.Local),
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken);
}
