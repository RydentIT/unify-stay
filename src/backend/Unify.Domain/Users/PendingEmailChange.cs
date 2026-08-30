using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>
/// A requested address change awaiting confirmation. The user's current address stays live the
/// whole time, so a typo in the new address never locks anyone out (BR-PRF-002).
/// </summary>
public sealed class PendingEmailChange : Entity
{
    public PendingEmailChange(
        Guid id,
        Guid userId,
        Email newEmail,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? usedAt = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(newEmail);

        if (userId == Guid.Empty)
        {
            throw new DomainException("PendingEmailChange must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("PendingEmailChange requires a token hash.");
        }

        UserId = userId;
        NewEmail = newEmail;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        UsedAt = usedAt;
    }

    public Guid UserId { get; }

    public Email NewEmail { get; }

    public string TokenHash { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsUsed => UsedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsRedeemable(DateTimeOffset now) => !IsUsed && !IsExpired(now);

    public void MarkUsed(DateTimeOffset now)
    {
        if (IsUsed)
        {
            throw new DomainException("This email change token has already been used.");
        }

        UsedAt = now;
    }
}
