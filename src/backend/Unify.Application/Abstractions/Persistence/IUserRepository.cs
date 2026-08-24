using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Persistence;

/// <summary>
/// Read/write access to the users aggregate. Implemented with hand-written SQL over Dapper
/// in Unify.Infrastructure - there is no ORM change-tracking, so every mutation is explicit.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Inserts the user together with its role grants and auth providers.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
