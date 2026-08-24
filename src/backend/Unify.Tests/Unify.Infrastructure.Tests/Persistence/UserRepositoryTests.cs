using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Tests.Infrastructure;

namespace Unify.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises the hand-written Dapper SQL against the real schema in database/sql/. These are
/// the tests that catch a column rename or a bad alias, which no unit test can.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public UserRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    private UserRepository CreateRepository() => new(_postgres.CreateConnectionFactory());

    private static User CreateUser(string email, out Guid id)
    {
        id = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var user = new User(
            id,
            Email.Create(email),
            passwordHash: "$2a$12$abcdefghijklmnopqrstuv",
            displayName: "Test Person",
            status: UserStatus.PendingVerification,
            mustChangePassword: false,
            createdAtUtc: now);

        user.AddRole(new UserRole(Guid.CreateVersion7(), id, RoleName.Guest, now));
        user.AddAuthProvider(new AuthProvider(
            Guid.CreateVersion7(), id, AuthProviderKind.Local, id.ToString(), now));

        return user;
    }

    [RequiresPostgresFact]
    public async Task Round_trips_a_user_with_its_roles_and_auth_providers()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User user = CreateUser("round.trip@example.com", out Guid id);

        await repository.AddAsync(user);

        User? loaded = await repository.GetByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal("round.trip@example.com", loaded.Email.Value);
        Assert.Equal("Test Person", loaded.DisplayName);
        Assert.Equal(UserStatus.PendingVerification, loaded.Status);
        Assert.False(loaded.MustChangePassword);
        Assert.False(loaded.IsEmailVerified);

        Assert.Equal(RoleName.Guest, Assert.Single(loaded.Roles).Role);
        Assert.Equal(AuthProviderKind.Local, Assert.Single(loaded.AuthProviders).Kind);
    }

    [RequiresPostgresFact]
    public async Task Finds_a_user_by_email()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        await repository.AddAsync(CreateUser("by.email@example.com", out Guid id));

        User? loaded = await repository.GetByEmailAsync(Email.Create("by.email@example.com"));

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded.Id);
    }

    [RequiresPostgresFact]
    public async Task Email_lookup_is_case_insensitive_because_the_domain_normalises_it()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        await repository.AddAsync(CreateUser("Mixed.Case@Example.COM", out _));

        Assert.True(await repository.ExistsByEmailAsync(Email.Create("mixed.case@example.com")));
        Assert.True(await repository.ExistsByEmailAsync(Email.Create("MIXED.CASE@EXAMPLE.COM")));
    }

    [RequiresPostgresFact]
    public async Task Reports_absence_for_an_unknown_address()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();

        Assert.False(await repository.ExistsByEmailAsync(Email.Create("nobody@example.com")));
        Assert.Null(await repository.GetByEmailAsync(Email.Create("nobody@example.com")));
        Assert.Null(await repository.GetByIdAsync(Guid.CreateVersion7()));
    }

    [RequiresPostgresFact]
    public async Task The_unique_email_index_rejects_a_duplicate_address()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        await repository.AddAsync(CreateUser("duplicate@example.com", out _));

        await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => repository.AddAsync(CreateUser("duplicate@example.com", out _)));
    }
}
