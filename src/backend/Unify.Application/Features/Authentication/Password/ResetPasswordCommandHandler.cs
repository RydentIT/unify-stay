using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Password;

/// <summary>
/// FPW-004 (unexpired), FPW-005 (unused), FPW-007 (not the current password) and
/// FPW-010/BR-FPW-004 (every session dies).
///
/// The session revocation is the security-critical step: a reset is what someone does when they
/// believe their password is compromised, so any session an attacker already holds has to stop
/// working at the same moment.
/// </summary>
internal sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private readonly IPasswordResetTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessions;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository tokens,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ISessionRepository sessions,
        ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _tokens = tokens;
        _users = users;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        string hash = _tokenGenerator.Hash(request.Token);

        PasswordResetToken? token = await _tokens
            .GetByHashAsync(hash, cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            await LogRejectedAsync(null, "unrecognised", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.InvalidResetToken);
        }

        DateTimeOffset now = _clock.UtcNow;

        // FPW-005: a spent token stays spent, so an intercepted link cannot be replayed.
        if (token.IsUsed)
        {
            await LogRejectedAsync(token.UserId, "already_used", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.UsedResetToken);
        }

        // FPW-004.
        if (token.IsExpired(now))
        {
            await LogRejectedAsync(token.UserId, "expired", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.ExpiredResetToken);
        }

        User? user = await _users.GetByIdAsync(token.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            await LogRejectedAsync(token.UserId, "account_unavailable", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.InvalidResetToken);
        }

        // FPW-007: reusing the existing password defeats the purpose of the reset.
        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return Result.Failure(AuthErrors.PasswordMatchesCurrent);
        }

        user.SetPassword(_passwordHasher.Hash(request.NewPassword), now);

        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _tokens.MarkUsedAsync(token.Id, now, cancellationToken).ConfigureAwait(false);

        // BR-FPW-004: every session, including any the attacker holds.
        await _sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.PasswordResetCompleted,
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

    private Task LogRejectedAsync(Guid? userId, string reason, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.PasswordResetTokenRejected,
                UserId = userId,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { reason },
            },
            cancellationToken);
}
