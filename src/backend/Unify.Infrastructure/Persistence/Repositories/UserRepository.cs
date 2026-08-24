using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Rows;

namespace Unify.Infrastructure.Persistence.Repositories;

/// <summary>
/// Hand-written SQL over Dapper. Postgres columns are snake_case and are aliased to the row
/// DTO property names at each query rather than through a global naming convention, so the
/// mapping stays visible where it is used.
/// </summary>
internal sealed class UserRepository : IUserRepository
{
    private const string SelectUserColumns = """
        SELECT  id                    AS Id,
                email                 AS Email,
                password_hash         AS PasswordHash,
                display_name          AS DisplayName,
                status                AS Status,
                must_change_password  AS MustChangePassword,
                email_verified_at_utc AS EmailVerifiedAtUtc,
                created_at_utc        AS CreatedAtUtc
        FROM    users
        """;

    private const string InsertUserSql = """
        INSERT INTO users (
            id, email, password_hash, display_name, status,
            must_change_password, email_verified_at_utc, created_at_utc, updated_at_utc)
        VALUES (
            @Id, @Email, @PasswordHash, @DisplayName, @Status,
            @MustChangePassword, @EmailVerifiedAtUtc, @CreatedAtUtc, @CreatedAtUtc);
        """;

    private const string InsertUserRoleSql = """
        INSERT INTO user_roles (id, user_id, role, granted_at_utc)
        VALUES (@Id, @UserId, @Role, @GrantedAtUtc)
        ON CONFLICT (user_id, role) DO NOTHING;
        """;

    private const string InsertAuthProviderSql = """
        INSERT INTO auth_providers (id, user_id, kind, provider_subject, linked_at_utc)
        VALUES (@Id, @UserId, @Kind, @ProviderSubject, @LinkedAtUtc);
        """;

    private const string SelectUserRolesSql = """
        SELECT id AS Id, user_id AS UserId, role AS Role, granted_at_utc AS GrantedAtUtc
        FROM   user_roles
        WHERE  user_id = @UserId;
        """;

    private const string SelectAuthProvidersSql = """
        SELECT id AS Id, user_id AS UserId, kind AS Kind,
               provider_subject AS ProviderSubject, linked_at_utc AS LinkedAtUtc
        FROM   auth_providers
        WHERE  user_id = @UserId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        UserRow? row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                SelectUserColumns + " WHERE id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null
            ? null
            : await HydrateAsync(connection, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        UserRow? row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                SelectUserColumns + " WHERE email = @Email;",
                new { Email = email.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null
            ? null
            : await HydrateAsync(connection, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM users WHERE email = @Email);",
                new { Email = email.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // The account and its grants must land together or not at all, so all three inserts
        // share one transaction instead of running as independent statements.
        await using DbTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertUserSql,
            new
            {
                user.Id,
                Email = user.Email.Value,
                user.PasswordHash,
                user.DisplayName,
                Status = (short)user.Status,
                user.MustChangePassword,
                user.EmailVerifiedAtUtc,
                user.CreatedAtUtc,
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (user.Roles.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertUserRoleSql,
                user.Roles.Select(role => new
                {
                    role.Id,
                    role.UserId,
                    Role = role.Role.ToString(),
                    role.GrantedAtUtc,
                }).ToArray(),
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        if (user.AuthProviders.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertAuthProviderSql,
                user.AuthProviders.Select(provider => new
                {
                    provider.Id,
                    provider.UserId,
                    Kind = (short)provider.Kind,
                    provider.ProviderSubject,
                    provider.LinkedAtUtc,
                }).ToArray(),
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<User> HydrateAsync(
        DbConnection connection,
        UserRow row,
        CancellationToken cancellationToken)
    {
        var user = new User(
            row.Id,
            Email.Create(row.Email),
            row.PasswordHash,
            row.DisplayName,
            (UserStatus)row.Status,
            row.MustChangePassword,
            row.CreatedAtUtc,
            row.EmailVerifiedAtUtc);

        IEnumerable<UserRoleRow> roleRows = await connection.QueryAsync<UserRoleRow>(
            new CommandDefinition(
                SelectUserRolesSql,
                new { UserId = row.Id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (UserRoleRow roleRow in roleRows)
        {
            // Roles are persisted by name. A value this build cannot parse means the database
            // holds a role we do not know about, which must fail loudly rather than be
            // silently dropped into a lesser set of permissions.
            if (!Enum.TryParse(roleRow.Role, ignoreCase: false, out RoleName role))
            {
                throw new InvalidOperationException(
                    $"User {row.Id} holds unrecognised role {roleRow.Role}.");
            }

            user.AddRole(new UserRole(roleRow.Id, roleRow.UserId, role, roleRow.GrantedAtUtc));
        }

        IEnumerable<AuthProviderRow> providerRows = await connection.QueryAsync<AuthProviderRow>(
            new CommandDefinition(
                SelectAuthProvidersSql,
                new { UserId = row.Id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (AuthProviderRow providerRow in providerRows)
        {
            user.AddAuthProvider(new AuthProvider(
                providerRow.Id,
                providerRow.UserId,
                (AuthProviderKind)providerRow.Kind,
                providerRow.ProviderSubject,
                providerRow.LinkedAtUtc));
        }

        return user;
    }
}
