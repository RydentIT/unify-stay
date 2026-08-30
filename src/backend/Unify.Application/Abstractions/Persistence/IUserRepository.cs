using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Persistence;

/// <summary>
/// Read/write access to the users aggregate, including its role grants and linked providers.
/// Implemented with hand-written SQL over Dapper - there is no change tracking, so every
/// mutation is an explicit call.
/// </summary>
public interface IUserRepository
{
    /// <summary>Loads the user with roles and auth providers populated.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads by address across ALL providers, which is what makes the uniqueness check in
    /// REG-003 and the linking decision in REG-004/REG-005 possible.
    /// </summary>
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Finds the account behind an external identity, e.g. a Google subject.</summary>
    Task<User?> GetByProviderAsync(
        AuthProviderKind provider,
        string providerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Inserts the user together with its role grants and auth providers, in one transaction.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Persists mutable columns and reconciles role grants.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task AddAuthProviderAsync(AuthProvider provider, CancellationToken cancellationToken = default);

    Task AddRoleAsync(Guid userId, RoleName role, CancellationToken cancellationToken = default);

    /// <summary>Backs the admin bootstrap idempotency check: never create a second Admin.</summary>
    Task<bool> ExistsAnyWithRoleAsync(RoleName role, CancellationToken cancellationToken = default);

    /// <summary>
    /// The admin user directory (Part 3): a paginated, filterable list. Returns flat rows rather
    /// than full <see cref="User"/> aggregates deliberately - the list view needs neither roles'
    /// full detail nor auth providers per row, and hydrating those for every row on every page
    /// would be wasted work.
    /// </summary>
    Task<UserDirectoryPage> SearchDirectoryAsync(
        UserDirectoryFilter filter,
        CancellationToken cancellationToken = default);
}

/// <summary>Search/filter/sort/paging input for <see cref="IUserRepository.SearchDirectoryAsync"/>.</summary>
public sealed record UserDirectoryFilter(
    string? Search,
    RoleName? Role,
    UserStatus? Status,
    int Page,
    int PageSize,
    bool OldestFirst = false)
{
    /// <summary>Clamped so a caller-supplied page size cannot force an unbounded query.</summary>
    public int EffectivePageSize => Math.Clamp(PageSize, 1, 100);

    public int EffectivePage => Math.Max(Page, 1);
}

/// <summary>One row of the directory list - enough to render the table without a per-row detail call.</summary>
public sealed record UserDirectoryEntry(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<RoleName> Roles,
    UserStatus Status,
    DateTimeOffset CreatedAt);

public sealed record UserDirectoryPage(
    IReadOnlyList<UserDirectoryEntry> Items,
    int TotalCount,
    int Page,
    int PageSize);
