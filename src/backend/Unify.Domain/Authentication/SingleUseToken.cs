using Unify.Domain.Common;

namespace Unify.Domain.Authentication;

/// <summary>
/// Shared behaviour for the time-boxed, single-use tokens (password reset, email
/// verification). Only the hash is ever held: the raw token goes out in one email and is never
/// recoverable from our storage.
///
/// The three states are kept distinct on purpose - expired, already used, and unrecognised are
/// separate outcomes that the verification flow has to report differently (EVR-010).
/// </summary>
public abstract class SingleUseToken : Entity
{
    protected SingleUseToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? usedAt)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A token must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A token requires a hash.");
        }

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        UsedAt = usedAt;
    }

    public Guid UserId { get; }

    public string TokenHash { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsUsed => UsedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Neither spent nor timed out, so it may still be redeemed.</summary>
    public bool IsRedeemable(DateTimeOffset now) => !IsUsed && !IsExpired(now);

    public void MarkUsed(DateTimeOffset now)
    {
        if (IsUsed)
        {
            throw new DomainException("This token has already been used.");
        }

        UsedAt = now;
    }

    /// <summary>
    /// Retires a token without it having been redeemed. Used when issuing a replacement, so an
    /// older link stops working the moment a new one is sent (BR-EVR-002).
    /// </summary>
    public void Invalidate(DateTimeOffset now) => UsedAt ??= now;
}
