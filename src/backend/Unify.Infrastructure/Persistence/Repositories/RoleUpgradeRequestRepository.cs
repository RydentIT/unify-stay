using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Settings;
using Unify.Infrastructure.Persistence.Rows;

namespace Unify.Infrastructure.Persistence.Repositories;

internal sealed class RoleUpgradeRequestRepository : IRoleUpgradeRequestRepository
{
    private const string SelectColumns = """
        SELECT id               AS Id,
               user_id          AS UserId,
               nic_number       AS NicNumber,
               address          AS Address,
               phone_number_2   AS PhoneNumber2,
               property_info    AS PropertyInfo,
               status           AS Status,
               rejection_reason AS RejectionReason,
               submitted_at     AS SubmittedAt,
               decided_at       AS DecidedAt,
               decided_by       AS DecidedBy
        FROM   role_upgrade_requests
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public RoleUpgradeRequestRepository(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task AddAsync(RoleUpgradeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO role_upgrade_requests
                (id, user_id, nic_number, address, phone_number_2, property_info,
                 status, rejection_reason, submitted_at, decided_at, decided_by)
            VALUES (@Id, @UserId, @NicNumber, @Address, @PhoneNumber2, @PropertyInfo,
                    @Status, @RejectionReason, @SubmittedAt, @DecidedAt, @DecidedBy);
            """,
            ToParameters(request),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<RoleUpgradeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        UpgradeRequestRow? row = await connection.QuerySingleOrDefaultAsync<UpgradeRequestRow>(
            new CommandDefinition(
                SelectColumns + " WHERE id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task<RoleUpgradeRequest?> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // Mirrors the partial unique index in migration 0010 (BR-SET-001).
        UpgradeRequestRow? row = await connection.QuerySingleOrDefaultAsync<UpgradeRequestRow>(
            new CommandDefinition(
                SelectColumns + " WHERE user_id = @UserId AND status IN ('pending', 'approved');",
                new { UserId = userId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task<RoleUpgradeRequest?> GetLatestForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        UpgradeRequestRow? row = await connection.QuerySingleOrDefaultAsync<UpgradeRequestRow>(
            new CommandDefinition(
                SelectColumns + " WHERE user_id = @UserId ORDER BY submitted_at DESC LIMIT 1;",
                new { UserId = userId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<RoleUpgradeRequest>> ListByStatusAsync(
        UpgradeRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<UpgradeRequestRow> rows = await connection.QueryAsync<UpgradeRequestRow>(
            new CommandDefinition(
                SelectColumns + " WHERE status = @Status ORDER BY submitted_at ASC;",
                new { Status = ToDb(status) },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(Map)];
    }

    public async Task<IReadOnlyList<RoleUpgradeRequest>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<UpgradeRequestRow> rows = await connection.QueryAsync<UpgradeRequestRow>(
            new CommandDefinition(
                SelectColumns + " WHERE user_id = @UserId ORDER BY submitted_at DESC;",
                new { UserId = userId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(Map)];
    }

    public async Task UpdateAsync(RoleUpgradeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE role_upgrade_requests
            SET    status           = @Status,
                   rejection_reason = @RejectionReason,
                   decided_at       = @DecidedAt,
                   decided_by       = @DecidedBy
            WHERE  id = @Id;
            """,
            ToParameters(request),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);

    private static object ToParameters(RoleUpgradeRequest request) => new
    {
        request.Id,
        request.UserId,
        request.NicNumber,
        request.Address,
        request.PhoneNumber2,
        request.PropertyInfo,
        Status = ToDb(request.Status),
        request.RejectionReason,
        request.SubmittedAt,
        request.DecidedAt,
        request.DecidedBy,
    };

    private static string ToDb(UpgradeRequestStatus status) => status.ToString().ToLowerInvariant();

    private static RoleUpgradeRequest Map(UpgradeRequestRow row) => new(
        row.Id,
        row.UserId,
        row.NicNumber,
        row.Address,
        row.PhoneNumber2,
        row.PropertyInfo,
        Enum.TryParse(row.Status, ignoreCase: true, out UpgradeRequestStatus status)
            ? status
            : throw new InvalidOperationException($"Unrecognised upgrade request status '{row.Status}'."),
        row.SubmittedAt,
        row.RejectionReason,
        row.DecidedAt,
        row.DecidedBy);
}
