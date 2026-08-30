using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Rows;

namespace Unify.Infrastructure.Persistence.Repositories;

internal sealed class UserSuspensionRepository : IUserSuspensionRepository
{
    private const string SelectColumns = """
        SELECT id           AS Id,
               user_id      AS UserId,
               reason       AS Reason,
               suspended_by AS SuspendedBy,
               suspended_at AS SuspendedAt,
               status       AS Status,
               lifted_by    AS LiftedBy,
               lifted_at    AS LiftedAt
        FROM   user_suspensions
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public UserSuspensionRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task AddAsync(UserSuspension suspension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suspension);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_suspensions
                (id, user_id, reason, suspended_by, suspended_at, status, lifted_by, lifted_at)
            VALUES (@Id, @UserId, @Reason, @SuspendedBy, @SuspendedAt, @Status, @LiftedBy, @LiftedAt);
            """,
            ToParameters(suspension),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<UserSuspension?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        UserSuspensionRow? row = await connection.QuerySingleOrDefaultAsync<UserSuspensionRow>(
            new CommandDefinition(
                SelectColumns + " WHERE user_id = @UserId AND status = 'active';",
                new { UserId = userId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<UserSuspension>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<UserSuspensionRow> rows = await connection.QueryAsync<UserSuspensionRow>(
            new CommandDefinition(
                SelectColumns + " WHERE user_id = @UserId ORDER BY suspended_at DESC;",
                new { UserId = userId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(Map)];
    }

    public async Task UpdateAsync(UserSuspension suspension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suspension);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_suspensions
            SET    status    = @Status,
                   lifted_by = @LiftedBy,
                   lifted_at = @LiftedAt
            WHERE  id = @Id;
            """,
            ToParameters(suspension),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);

    private static object ToParameters(UserSuspension suspension) => new
    {
        suspension.Id,
        suspension.UserId,
        suspension.Reason,
        suspension.SuspendedBy,
        suspension.SuspendedAt,
        Status = ToDb(suspension.Status),
        suspension.LiftedBy,
        suspension.LiftedAt,
    };

    private static string ToDb(SuspensionStatus status) => status.ToString().ToLowerInvariant();

    private static UserSuspension Map(UserSuspensionRow row) => new(
        row.Id,
        row.UserId,
        row.Reason,
        row.SuspendedBy,
        row.SuspendedAt,
        Enum.TryParse(row.Status, ignoreCase: true, out SuspensionStatus status)
            ? status
            : throw new InvalidOperationException($"Unrecognised suspension status '{row.Status}'."),
        row.LiftedBy,
        row.LiftedAt);
}
