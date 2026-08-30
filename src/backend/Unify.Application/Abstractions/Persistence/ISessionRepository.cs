using Unify.Domain.Authentication;

namespace Unify.Application.Abstractions.Persistence;

/// <summary>
/// Server-side sessions. Every "invalidate sessions" rule in the specs resolves to a call here
/// - client-side token deletion is never sufficient (BR-LOG-006).
/// </summary>
public interface ISessionRepository
{
    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    Task<Session?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes one session, e.g. an explicit logout.</summary>
    Task RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every live session for a user. Used after a password reset (BR-FPW-004) and on
    /// deactivate/delete (SET-013).
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every live session EXCEPT one. Used by change-password from the profile, which
    /// must not sign the user out of the tab they are using (BR-PRF-003).
    /// </summary>
    Task RevokeAllForUserExceptAsync(
        Guid userId,
        Guid exceptSessionId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
