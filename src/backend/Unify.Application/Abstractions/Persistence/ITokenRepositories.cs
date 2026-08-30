using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Persistence;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task MarkUsedAsync(Guid tokenId, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    /// <summary>Retires outstanding tokens so only the newest link works.</summary>
    Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);

    Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task MarkUsedAsync(Guid tokenId, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    /// <summary>Invalidates any previous unexpired token before a new one is issued (BR-EVR-002).</summary>
    Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Supports the resend rate limit (EVR-006).</summary>
    Task<int> CountIssuedSinceAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default);
}

public interface IPendingEmailChangeRepository
{
    Task AddAsync(PendingEmailChange change, CancellationToken cancellationToken = default);

    Task<PendingEmailChange?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<PendingEmailChange?> GetActiveForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkUsedAsync(Guid changeId, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Guards against pointing two pending changes at the same destination address.</summary>
    Task<bool> IsEmailTakenAsync(Email email, CancellationToken cancellationToken = default);
}
