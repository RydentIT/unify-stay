namespace Unify.Domain.Authentication;

/// <summary>Single-use password reset token (FPW-004, FPW-005).</summary>
public sealed class PasswordResetToken : SingleUseToken
{
    public PasswordResetToken(
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
