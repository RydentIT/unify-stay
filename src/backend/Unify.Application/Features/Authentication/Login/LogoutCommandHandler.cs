using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Authentication;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// BR-LOG-006: logout is a server-side operation. Dropping the token in the browser leaves the
/// refresh token valid until expiry, so the session row is revoked here.
/// </summary>
internal sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, Result>
{
    private readonly ISessionRepository _sessions;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public LogoutCommandHandler(
        ISessionRepository sessions,
        ISecureTokenGenerator tokenGenerator,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _sessions = sessions;
        _tokenGenerator = tokenGenerator;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(LogoutCommand request, CancellationToken cancellationToken)
    {
        Guid? userId = _currentUser.UserId;
        DateTimeOffset now = _clock.UtcNow;

        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            string hash = _tokenGenerator.Hash(request.RefreshToken);

            Session? session = await _sessions
                .GetByRefreshTokenHashAsync(hash, cancellationToken)
                .ConfigureAwait(false);

            // Only revoke a session that belongs to the caller, so a stolen or guessed refresh
            // token cannot be used to sign somebody else out.
            if (session is not null && (userId is null || session.UserId == userId))
            {
                await _sessions.RevokeAsync(session.Id, now, cancellationToken).ConfigureAwait(false);
                userId ??= session.UserId;
            }
        }
        else if (userId is not null)
        {
            await _sessions.RevokeAllForUserAsync(userId.Value, now, cancellationToken).ConfigureAwait(false);
        }

        if (userId is not null)
        {
            await _auditLogger.LogAsync(
                new AuditEvent
                {
                    ActionType = AuditActions.Logout,
                    UserId = userId,
                    Email = _currentUser.Email,
                    IpAddress = _currentUser.IpAddress,
                },
                cancellationToken).ConfigureAwait(false);
        }

        // Always succeeds: an already-expired or unknown token is not something the caller can
        // act on, and reporting it would leak whether the token was real.
        return Result.Success();
    }
}
