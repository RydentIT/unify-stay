using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Rows;

namespace Unify.Infrastructure.Persistence.Repositories;

/// <summary>
/// Hand-written SQL over Dapper. Postgres columns are snake_case and aliased to the row DTO
/// property names at each query, so the mapping stays visible where it is used rather than
/// depending on a global naming convention.
///
/// Enum-like columns are stored lower-cased ('active', 'google') to match the CHECK constraints
/// in the migrations; conversion happens in the two helpers at the bottom.
/// </summary>
internal sealed class UserRepository : IUserRepository
{
    private const string SelectUserColumns = """
        SELECT  id                    AS Id,
                first_name            AS FirstName,
                last_name             AS LastName,
                email                 AS Email,
                password_hash         AS PasswordHash,
                email_verified        AS EmailVerified,
                must_change_password  AS MustChangePassword,
                must_complete_profile AS MustCompleteProfile,
                status                AS Status,
                avatar_url            AS AvatarUrl,
                contact_number        AS ContactNumber,
                pending_email         AS PendingEmail,
                created_at            AS CreatedAt,
                updated_at            AS UpdatedAt
        FROM    users
        """;

    private const string InsertUserSql = """
        INSERT INTO users (
            id, first_name, last_name, email, password_hash, email_verified, must_change_password,
            must_complete_profile, status, avatar_url, contact_number, pending_email,
            created_at, updated_at)
        VALUES (
            @Id, @FirstName, @LastName, @Email, @PasswordHash, @EmailVerified, @MustChangePassword,
            @MustCompleteProfile, @Status, @AvatarUrl, @ContactNumber, @PendingEmail,
            @CreatedAt, @UpdatedAt);
        """;

    private const string UpdateUserSql = """
        UPDATE users
        SET    first_name            = @FirstName,
               last_name             = @LastName,
               email                 = @Email,
               password_hash         = @PasswordHash,
               email_verified        = @EmailVerified,
               must_change_password  = @MustChangePassword,
               must_complete_profile = @MustCompleteProfile,
               status                = @Status,
               avatar_url            = @AvatarUrl,
               contact_number        = @ContactNumber,
               pending_email         = @PendingEmail,
               updated_at            = @UpdatedAt
        WHERE  id = @Id;
        """;

    private const string InsertAuthProviderSql = """
        INSERT INTO auth_providers (id, user_id, provider, provider_user_id, email_verified_by_provider, linked_at)
        VALUES (@Id, @UserId, @Provider, @ProviderUserId, @EmailVerifiedByProvider, @LinkedAt)
        ON CONFLICT (user_id, provider) DO NOTHING;
        """;

    private const string InsertUserRoleSql = """
        INSERT INTO user_roles (user_id, role_id, granted_at)
        VALUES (@UserId, @RoleId, @GrantedAt)
        ON CONFLICT (user_id, role_id) DO NOTHING;
        """;

    private const string SelectRolesSql = """
        SELECT r.name
        FROM   user_roles ur
        JOIN   roles r ON r.id = ur.role_id
        WHERE  ur.user_id = @UserId;
        """;

    private const string SelectProvidersSql = """
        SELECT id                         AS Id,
               user_id                    AS UserId,
               provider                   AS Provider,
               provider_user_id           AS ProviderUserId,
               email_verified_by_provider AS EmailVerifiedByProvider,
               linked_at                  AS LinkedAt
        FROM   auth_providers
        WHERE  user_id = @UserId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        UserRow? row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                SelectUserColumns + " WHERE id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : await HydrateAsync(connection, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        UserRow? row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                SelectUserColumns + " WHERE email = @Email;",
                new { Email = email.Value },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : await HydrateAsync(connection, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByProviderAsync(
        AuthProviderKind provider,
        string providerUserId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        UserRow? row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                """
                SELECT  u.id                   AS Id,
                        u.first_name           AS FirstName,
                        u.last_name            AS LastName,
                        u.email                AS Email,
                        u.password_hash        AS PasswordHash,
                        u.email_verified       AS EmailVerified,
                        u.must_change_password AS MustChangePassword,
                        u.must_complete_profile AS MustCompleteProfile,
                        u.status               AS Status,
                        u.avatar_url           AS AvatarUrl,
                        u.contact_number       AS ContactNumber,
                        u.pending_email        AS PendingEmail,
                        u.created_at           AS CreatedAt,
                        u.updated_at           AS UpdatedAt
                FROM    users u
                JOIN    auth_providers ap ON ap.user_id = u.id
                WHERE   ap.provider = @Provider AND ap.provider_user_id = @ProviderUserId;
                """,
                new { Provider = ToDb(provider), ProviderUserId = providerUserId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : await HydrateAsync(connection, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM users WHERE email = @Email);",
                new { Email = email.Value },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // The account, its roles and its providers must land together or not at all.
        await using DbTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertUserSql,
            ToParameters(user),
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (user.Roles.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertUserRoleSql,
                user.Roles.Select(role => new
                {
                    UserId = user.Id,
                    RoleId = (int)role,
                    GrantedAt = user.CreatedAt,
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
                    Provider = ToDb(provider.Provider),
                    provider.ProviderUserId,
                    provider.EmailVerifiedByProvider,
                    provider.LinkedAt,
                }).ToArray(),
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await using DbTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            UpdateUserSql,
            ToParameters(user),
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Roles are additive here. Revoking a role is a separate, deliberate operation rather
        // than a side effect of saving a profile edit.
        if (user.Roles.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertUserRoleSql,
                user.Roles.Select(role => new
                {
                    UserId = user.Id,
                    RoleId = (int)role,
                    GrantedAt = user.UpdatedAt,
                }).ToArray(),
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAuthProviderAsync(AuthProvider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertAuthProviderSql,
            new
            {
                provider.Id,
                provider.UserId,
                Provider = ToDb(provider.Provider),
                provider.ProviderUserId,
                provider.EmailVerifiedByProvider,
                provider.LinkedAt,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AddRoleAsync(Guid userId, RoleName role, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertUserRoleSql,
            new { UserId = userId, RoleId = (int)role, GrantedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAnyWithRoleAsync(RoleName role, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM user_roles WHERE role_id = @RoleId);",
            new { RoleId = (int)role },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<UserDirectoryPage> SearchDirectoryAsync(
        UserDirectoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        int page = filter.EffectivePage;
        int pageSize = filter.EffectivePageSize;
        int offset = (page - 1) * pageSize;

        string? search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        string? status = filter.Status is null ? null : ToDb(filter.Status.Value);
        int? roleId = filter.Role is null ? null : (int)filter.Role.Value;
        string orderDirection = filter.OldestFirst ? "ASC" : "DESC";

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // A WITH-clause CTE is scoped to a single statement, so it cannot be shared across the
        // count and the page in one multi-statement command (Postgres: "relation does not
        // exist" on the second statement). Two round trips over the same WHERE predicate instead.
        const string wherePredicate = """
            WHERE  (@Search IS NULL
                        OR u.first_name ILIKE '%' || @Search || '%'
                        OR u.last_name ILIKE '%' || @Search || '%'
                        OR u.email ILIKE '%' || @Search || '%')
              AND  (@Status IS NULL OR u.status = @Status)
              AND  (@RoleId IS NULL
                        OR EXISTS (
                            SELECT 1 FROM user_roles ur
                            WHERE ur.user_id = u.id AND ur.role_id = @RoleId))
            """;

        object parameters = new { Search = search, Status = status, RoleId = roleId, PageSize = pageSize, Offset = offset };

        int totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM users u {wherePredicate};",
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Role names are aggregated per row rather than joined-and-grouped, since a user with
        // multiple roles must still produce exactly one directory row.
        IEnumerable<UserDirectoryRow> rows = await connection.QueryAsync<UserDirectoryRow>(new CommandDefinition(
            $"""
            SELECT u.id                                                                AS Id,
                   u.first_name                                                        AS FirstName,
                   u.last_name                                                         AS LastName,
                   u.email                                                             AS Email,
                   u.status                                                            AS Status,
                   u.created_at                                                        AS CreatedAt,
                   (SELECT string_agg(r.name, ',')
                    FROM   user_roles ur
                    JOIN   roles r ON r.id = ur.role_id
                    WHERE  ur.user_id = u.id)                                          AS RoleNames
            FROM   users u
            {wherePredicate}
            ORDER BY u.created_at {orderDirection}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        UserDirectoryEntry[] entries = [.. rows.Select(row => new UserDirectoryEntry(
            row.Id,
            row.FirstName,
            row.LastName,
            row.Email,
            [.. (row.RoleNames ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => Enum.Parse<RoleName>(name, ignoreCase: true))],
            ParseStatus(row.Status),
            row.CreatedAt))];

        return new UserDirectoryPage(entries, totalCount, page, pageSize);
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);

    private static object ToParameters(User user) => new
    {
        user.Id,
        user.FirstName,
        user.LastName,
        Email = user.Email.Value,
        user.PasswordHash,
        user.EmailVerified,
        user.MustChangePassword,
        user.MustCompleteProfile,
        Status = ToDb(user.Status),
        user.AvatarUrl,
        user.ContactNumber,
        user.PendingEmail,
        user.CreatedAt,
        user.UpdatedAt,
    };

    private static async Task<User> HydrateAsync(
        DbConnection connection,
        UserRow row,
        CancellationToken cancellationToken)
    {
        var user = new User(
            row.Id,
            row.FirstName,
            row.LastName,
            Email.Create(row.Email),
            row.PasswordHash,
            row.EmailVerified,
            row.MustChangePassword,
            ParseStatus(row.Status),
            row.CreatedAt,
            row.UpdatedAt,
            row.AvatarUrl,
            row.ContactNumber,
            row.PendingEmail,
            row.MustCompleteProfile);

        IEnumerable<string> roleNames = await connection.QueryAsync<string>(
            new CommandDefinition(
                SelectRolesSql,
                new { UserId = row.Id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (string roleName in roleNames)
        {
            // Roles are persisted by name. A value this build cannot parse means the database
            // holds a role we do not know about, which must fail loudly rather than silently
            // downgrade the user to a smaller set of permissions.
            if (!Enum.TryParse(roleName, out RoleName role))
            {
                throw new InvalidOperationException($"User {row.Id} holds unrecognised role {roleName}.");
            }

            user.AddRole(role);
        }

        IEnumerable<AuthProviderRow> providerRows = await connection.QueryAsync<AuthProviderRow>(
            new CommandDefinition(
                SelectProvidersSql,
                new { UserId = row.Id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (AuthProviderRow providerRow in providerRows)
        {
            user.AddAuthProvider(new AuthProvider(
                providerRow.Id,
                providerRow.UserId,
                ParseProvider(providerRow.Provider),
                providerRow.ProviderUserId,
                providerRow.EmailVerifiedByProvider,
                providerRow.LinkedAt));
        }

        return user;
    }

    private static string ToDb(UserStatus status) => status.ToString().ToLowerInvariant();

    private static string ToDb(AuthProviderKind provider) => provider.ToString().ToLowerInvariant();

    private static UserStatus ParseStatus(string value) =>
        Enum.TryParse(value, ignoreCase: true, out UserStatus status)
            ? status
            : throw new InvalidOperationException($"Unrecognised user status '{value}'.");

    private static AuthProviderKind ParseProvider(string value) =>
        Enum.TryParse(value, ignoreCase: true, out AuthProviderKind provider)
            ? provider
            : throw new InvalidOperationException($"Unrecognised auth provider '{value}'.");
}
