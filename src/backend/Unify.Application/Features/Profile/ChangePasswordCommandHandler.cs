using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

/// <summary>
/// Self-service password change (PRF-004, PRF-005, PRF-010, PRF-012).
///
/// The session handling differs from a reset on purpose: a reset revokes everything
/// (BR-FPW-004) because the old password is presumed compromised, whereas this flow proves
/// possession of the current password first, so it keeps the caller's own session alive and
/// only signs out the others (BR-PRF-003).
/// </summary>
internal sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, Result>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessions;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ChangePasswordCommandHandler(
        CurrentUserAccessor currentUserAccessor,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ISessionRepository sessions,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _currentUserAccessor = currentUserAccessor;
        _users = users;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        User user = resolved.Value;

        // PRF-012 / BR-PRF-004: there is no password to replace on a Google-only account, and
        // silently creating one would be a surprising way to gain a second credential.
        if (!user.HasPassword)
        {
            return Result.Failure(AuthErrors.NoPasswordForGoogleAccount);
        }

        // PRF-004 / BR-PRF-001: prove possession before allowing a change, so a hijacked session
        // alone is not enough to lock the real owner out.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            await _auditLogger.LogAsync(
                new AuditEvent
                {
                    ActionType = AuditActions.LoginFailed,
                    UserId = user.Id,
                    Email = user.Email.Value,
                    IpAddress = _currentUser.IpAddress,
                    FieldsChanged = new { reason = "change_password_current_incorrect" },
                },
                cancellationToken).ConfigureAwait(false);

            return Result.Failure(AuthErrors.CurrentPasswordIncorrect);
        }

        // PRF-005.
        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return Result.Failure(AuthErrors.PasswordMatchesCurrent);
        }

        DateTimeOffset now = _clock.UtcNow;

        user.SetPassword(_passwordHasher.Hash(request.NewPassword), now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        // PRF-010 / BR-PRF-003: sign out everywhere else, but not the tab doing the changing.
        // With no identifiable current session, revoking all of them is the safer reading.
        if (_currentUser.SessionId is Guid currentSessionId)
        {
            await _sessions
                .RevokeAllForUserExceptAsync(user.Id, currentSessionId, now, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);
        }

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.PasswordChanged,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildPasswordChangedEmail(user.Email.Value, user.FullName),
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
