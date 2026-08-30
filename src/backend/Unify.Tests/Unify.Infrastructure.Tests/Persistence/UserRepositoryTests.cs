using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Tests.Infrastructure;

namespace Unify.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises the hand-written Dapper SQL against the real schema. These are the tests that
/// catch a column rename, a bad alias or an enum mapped the wrong way - none of which a unit
/// test with a substituted repository could ever see.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public UserRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    private UserRepository CreateRepository() => new(_postgres.CreateConnectionFactory());

    private static User NewUser(string email, string? passwordHash = "hashed-password")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var user = new User(
            Guid.CreateVersion7(),
            "Integration",
            "User",
            Email.Create(email),
            passwordHash,
            emailVerified: false,
            mustChangePassword: false,
            UserStatus.Active,
            now,
            now);

        user.AddRole(RoleName.Student);
        user.AddAuthProvider(AuthProvider.Local(Guid.CreateVersion7(), user.Id, now));

        return user;
    }

    [RequiresPostgresFact]
    public async Task Round_trips_a_user_with_roles_and_providers()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User user = NewUser("roundtrip@example.com");

        await repository.AddAsync(user);

        User? loaded = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.Equal("roundtrip@example.com", loaded.Email.Value);
        Assert.Equal("Integration", loaded.FirstName);
        Assert.Equal("User", loaded.LastName);
        Assert.Equal(UserStatus.Active, loaded.Status);
        Assert.False(loaded.EmailVerified);
        Assert.True(loaded.HasPassword);
        Assert.Equal(RoleName.Student, Assert.Single(loaded.Roles));
        Assert.Equal(AuthProviderKind.Local, Assert.Single(loaded.AuthProviders).Provider);
    }

    /// <summary>REG-003 depends on this lookup spanning providers.</summary>
    [RequiresPostgresFact]
    public async Task Finds_a_user_by_email_regardless_of_provider()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User googleUser = NewUser("google-user@example.com", passwordHash: null);
        googleUser.AddAuthProvider(AuthProvider.Google(
            Guid.CreateVersion7(), googleUser.Id, "google-subject-abc", true, DateTimeOffset.UtcNow));

        await repository.AddAsync(googleUser);

        User? byEmail = await repository.GetByEmailAsync(Email.Create("google-user@example.com"));
        User? byProvider = await repository.GetByProviderAsync(AuthProviderKind.Google, "google-subject-abc");

        Assert.NotNull(byEmail);
        Assert.NotNull(byProvider);
        Assert.Equal(byEmail.Id, byProvider.Id);
        Assert.False(byEmail.HasPassword);
    }

    [RequiresPostgresFact]
    public async Task Email_lookup_is_case_insensitive_because_the_domain_normalises_it()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        await repository.AddAsync(NewUser("Mixed.Case@Example.COM"));

        Assert.True(await repository.ExistsByEmailAsync(Email.Create("mixed.case@example.com")));
        Assert.True(await repository.ExistsByEmailAsync(Email.Create("MIXED.CASE@EXAMPLE.COM")));
    }

    [RequiresPostgresFact]
    public async Task The_unique_email_index_rejects_a_duplicate()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        await repository.AddAsync(NewUser("duplicate@example.com"));

        await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => repository.AddAsync(NewUser("duplicate@example.com")));
    }

    /// <summary>Covers the enum/bool/nullable columns that a hand-written UPDATE easily gets wrong.</summary>
    [RequiresPostgresFact]
    public async Task Persists_status_verification_and_profile_changes()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User user = NewUser("mutate@example.com");
        await repository.AddAsync(user);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        user.MarkEmailVerified(now);
        user.UpdateProfile("Renamed", "Person", "https://cdn.example/a.png", "+94 71 234 5678", now);
        user.RequirePasswordChange(now);
        user.Deactivate(now);

        await repository.UpdateAsync(user);

        User? loaded = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.True(loaded.EmailVerified);
        Assert.True(loaded.MustChangePassword);
        Assert.Equal(UserStatus.Deactivated, loaded.Status);
        Assert.Equal("Renamed", loaded.FirstName);
        Assert.Equal("Person", loaded.LastName);
        Assert.Equal("https://cdn.example/a.png", loaded.AvatarUrl);
        Assert.Equal("+94 71 234 5678", loaded.ContactNumber);
    }

    /// <summary>SET-009: the approval path adds a role without disturbing the existing one.</summary>
    [RequiresPostgresFact]
    public async Task Adding_a_role_keeps_the_existing_roles()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User user = NewUser("upgrade@example.com");
        await repository.AddAsync(user);

        await repository.AddRoleAsync(user.Id, RoleName.PropertyOwner);

        User? loaded = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.Contains(RoleName.Student, loaded.Roles);
        Assert.Contains(RoleName.PropertyOwner, loaded.Roles);
    }

    /// <summary>Adding the same role twice must be idempotent, not a primary key violation.</summary>
    [RequiresPostgresFact]
    public async Task Granting_the_same_role_twice_is_harmless()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User user = NewUser("idempotent-role@example.com");
        await repository.AddAsync(user);

        await repository.AddRoleAsync(user.Id, RoleName.PropertyOwner);
        await repository.AddRoleAsync(user.Id, RoleName.PropertyOwner);

        User? loaded = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Roles.Count);
    }

    /// <summary>REG-004 links a second provider onto an existing account.</summary>
    [RequiresPostgresFact]
    public async Task Links_an_additional_auth_provider()
    {
        await _postgres.ResetAsync();

        UserRepository repository = CreateRepository();
        User user = NewUser("linkme@example.com");
        await repository.AddAsync(user);

        await repository.AddAuthProviderAsync(AuthProvider.Google(
            Guid.CreateVersion7(), user.Id, "google-sub-link", true, DateTimeOffset.UtcNow));

        User? loaded = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.AuthProviders.Count);
        Assert.True(loaded.HasProvider(AuthProviderKind.Google));
        Assert.True(loaded.HasProvider(AuthProviderKind.Local));
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
}
