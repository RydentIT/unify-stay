namespace Unify.Domain.Authentication;

/// <summary>Single-use email verification token (EVR-005, EVR-010).</summary>
public sealed class EmailVerificationToken : SingleUseToken
{
    public EmailVerificationToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? usedAt = null)
        : base(id, userId, tokenHash, expiresAt, createdAt, usedAt)
    {
    }
}
