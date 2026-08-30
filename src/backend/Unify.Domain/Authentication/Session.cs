using Unify.Domain.Common;
using Unify.Domain.Users;

namespace Unify.Domain.Authentication;

/// <summary>
/// A server-side refresh session. Logout and the various "invalidate all sessions" rules act
/// on these rows; clearing a token in the browser is never sufficient on its own (BR-LOG-006).
/// </summary>
public sealed class Session : Entity
{
    public Session(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        RoleName? roleClaim,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? revokedAt = null)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A session must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            throw new DomainException("A session requires a refresh token hash.");
        }

        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        RoleClaim = roleClaim;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        RevokedAt = revokedAt;
    }

    public Guid UserId { get; }

    /// <summary>SHA-256 of the refresh token. The raw value is handed to the client exactly once.</summary>
    public string RefreshTokenHash { get; }

    /// <summary>Role captured when the session was issued, so a stale session cannot carry new privileges.</summary>
    public RoleName? RoleClaim { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
