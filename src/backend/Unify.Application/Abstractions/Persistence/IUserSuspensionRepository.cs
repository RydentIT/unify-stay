using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Persistence;

public interface IUserSuspensionRepository
{
    Task AddAsync(UserSuspension suspension, CancellationToken cancellationToken = default);

    /// <summary>The user's current ACTIVE suspension, if any. Null means the user is not suspended.</summary>
    Task<UserSuspension?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Every suspension ever imposed on this user, most recent first, lifted ones included.</summary>
    Task<IReadOnlyList<UserSuspension>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserSuspension suspension, CancellationToken cancellationToken = default);
}
