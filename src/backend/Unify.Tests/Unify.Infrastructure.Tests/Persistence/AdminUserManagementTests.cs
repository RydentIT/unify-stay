using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Tests.Infrastructure;

namespace Unify.Infrastructure.Tests.Persistence;

/// <summary>
/// Part 3 (admin user directory search/filter/pagination) and Part 4 (suspension round-trip)
/// SQL. Both lean on real Postgres: the directory query's ILIKE/EXISTS/window logic and the
/// suspension table's partial unique index cannot be validated against a mock.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminUserManagementTests
{
    private readonly PostgresFixture _postgres;

    public AdminUserManagementTests(PostgresFixture postgres) => _postgres = postgres;

    private UserRepository Users => new(_postgres.CreateConnectionFactory());

    private async Task<User> SeedUserAsync(
        string email,
        RoleName role = RoleName.Student,
        UserStatus status = UserStatus.Active,
        DateTimeOffset? createdAt = null,
        string firstName = "Test",
        string lastName = "User")
    {
        DateTimeOffset now = createdAt ?? DateTimeOffset.UtcNow;

        var user = new User(
            Guid.CreateVersion7(), firstName, lastName, Email.Create(email), "hashed",
            emailVerified: true, mustChangePassword: false, status, now, now);

        user.AddRole(role);

        await Users.AddAsync(user);
        return user;
    }

    // ---------------------------------------------------------------- Suspensions

    [RequiresPostgresFact]
    public async Task Suspension_round_trips_and_reports_active_until_lifted()
    {
        await _postgres.ResetAsync();
        User target = await SeedUserAsync("suspend-target@example.com");
        User admin = await SeedUserAsync("suspend-admin@example.com", RoleName.Admin);

        var suspensions = new UserSuspensionRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserSuspension suspension = UserSuspension.Impose(
            Guid.CreateVersion7(), target.Id, "Repeated policy violations", admin.Id, now);
        await suspensions.AddAsync(suspension);

        UserSuspension? active = await suspensions.GetActiveForUserAsync(target.Id);
        Assert.NotNull(active);
        Assert.Equal("Repeated policy violations", active.Reason);
        Assert.True(active.IsActive);

        active.Lift(admin.Id, now.AddHours(1));
        await suspensions.UpdateAsync(active);

        Assert.Null(await suspensions.GetActiveForUserAsync(target.Id));

        IReadOnlyList<UserSuspension> history = await suspensions.ListForUserAsync(target.Id);
        UserSuspension historical = Assert.Single(history);
        Assert.Equal(SuspensionStatus.Lifted, historical.Status);
        Assert.Equal(admin.Id, historical.LiftedBy);
    }

    /// <summary>Mirrors the one-active-request pattern: at most one ACTIVE suspension per user.</summary>
    [RequiresPostgresFact]
    public async Task The_database_refuses_a_second_active_suspension()
    {
        await _postgres.ResetAsync();
        User target = await SeedUserAsync("double-suspend@example.com");
        User admin = await SeedUserAsync("double-suspend-admin@example.com", RoleName.Admin);

        var suspensions = new UserSuspensionRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await suspensions.AddAsync(UserSuspension.Impose(Guid.CreateVersion7(), target.Id, "First reason", admin.Id, now));

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => suspensions.AddAsync(
            UserSuspension.Impose(Guid.CreateVersion7(), target.Id, "Second reason", admin.Id, now)));
    }

    // ---------------------------------------------------------------- Directory search/filter/pagination

    [RequiresPostgresFact]
    public async Task Directory_search_matches_partial_case_insensitive_name_or_email()
    {
        await _postgres.ResetAsync();
        await SeedUserAsync("annabelle@example.com", firstName: "Annabelle", lastName: "Owner");
        await SeedUserAsync("someone-else@example.com", firstName: "Someone", lastName: "Else");

        UserDirectoryPage page = await Users.SearchDirectoryAsync(
            new UserDirectoryFilter(Search: "anna", Role: null, Status: null, Page: 1, PageSize: 20));

        UserDirectoryEntry entry = Assert.Single(page.Items);
        Assert.Equal("annabelle@example.com", entry.Email);
    }

    [RequiresPostgresFact]
    public async Task Directory_filters_by_role_and_status_independently()
    {
        await _postgres.ResetAsync();
        User owner = await SeedUserAsync("owner@example.com", RoleName.PropertyOwner);
        await SeedUserAsync("student@example.com", RoleName.Student);
        User suspendedStudent = await SeedUserAsync(
            "suspended-student@example.com", RoleName.Student, UserStatus.Suspended);

        UserDirectoryPage byRole = await Users.SearchDirectoryAsync(
            new UserDirectoryFilter(null, RoleName.PropertyOwner, null, 1, 20));
        Assert.Equal(owner.Id, Assert.Single(byRole.Items).Id);

        UserDirectoryPage byStatus = await Users.SearchDirectoryAsync(
            new UserDirectoryFilter(null, null, UserStatus.Suspended, 1, 20));
        Assert.Equal(suspendedStudent.Id, Assert.Single(byStatus.Items).Id);
    }

    [RequiresPostgresFact]
    public async Task Directory_paginates_with_a_correct_total_count()
    {
        await _postgres.ResetAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            await SeedUserAsync($"paged-{i}@example.com", createdAt: now.AddMinutes(i));
        }

        UserDirectoryPage firstPage = await Users.SearchDirectoryAsync(
            new UserDirectoryFilter(null, null, null, Page: 1, PageSize: 2));

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        // Default sort is newest first: the last-seeded user (i = 4) leads.
        Assert.Equal("paged-4@example.com", firstPage.Items[0].Email);

        UserDirectoryPage lastPage = await Users.SearchDirectoryAsync(
            new UserDirectoryFilter(null, null, null, Page: 3, PageSize: 2));

        Assert.Equal(5, lastPage.TotalCount);
        Assert.Single(lastPage.Items);
        Assert.Equal("paged-0@example.com", lastPage.Items[0].Email);
    }

    [RequiresPostgresFact]
    public async Task Directory_reports_every_role_a_user_holds()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync("multi-role@example.com", RoleName.Student);
        await Users.AddRoleAsync(user.Id, RoleName.PropertyOwner);

        UserDirectoryPage page = await Users.SearchDirectoryAsync(
            new UserDirectoryFilter(user.Email.Value, null, null, 1, 20));

        UserDirectoryEntry entry = Assert.Single(page.Items);
        Assert.Contains(RoleName.Student, entry.Roles);
        Assert.Contains(RoleName.PropertyOwner, entry.Roles);
    }
}
