using Unify.Domain.Settings;

namespace Unify.Application.Abstractions.Persistence;

public interface IRoleUpgradeRequestRepository
{
    Task AddAsync(RoleUpgradeRequest request, CancellationToken cancellationToken = default);

    Task<RoleUpgradeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The user's pending-or-approved request, if any. BR-SET-001 allows only one at a time, so
    /// a non-null result blocks a new submission.
    /// </summary>
    Task<RoleUpgradeRequest?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Most recent request regardless of status - SET-008 needs to see a rejection.</summary>
    Task<RoleUpgradeRequest?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleUpgradeRequest>> ListByStatusAsync(
        UpgradeRequestStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>Every request a user has ever submitted, most recent first. Feeds the admin user
    /// detail page's property-owner-request history.</summary>
    Task<IReadOnlyList<RoleUpgradeRequest>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(RoleUpgradeRequest request, CancellationToken cancellationToken = default);
}
