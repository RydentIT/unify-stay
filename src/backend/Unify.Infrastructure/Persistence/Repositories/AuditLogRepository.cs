using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Auditing;

namespace Unify.Infrastructure.Persistence.Repositories;

/// <summary>Append-only writer for audit_logs. Deliberately offers no update or delete.</summary>
internal sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_logs
                (id, user_id, email, provider, ip_address, action_type, fields_changed, occurred_at)
            VALUES (@Id, @UserId, @Email, @Provider, @IpAddress, @ActionType, @FieldsChanged, @OccurredAt);
            """,
            new
            {
                entry.Id,
                entry.UserId,
                entry.Email,
                entry.Provider,
                entry.IpAddress,
                entry.ActionType,
                entry.FieldsChanged,
                entry.OccurredAt,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<AuditRow> rows = await connection.QueryAsync<AuditRow>(new CommandDefinition(
            """
            SELECT id             AS Id,
                   user_id        AS UserId,
                   email          AS Email,
                   provider       AS Provider,
                   ip_address     AS IpAddress,
                   action_type    AS ActionType,
                   fields_changed AS FieldsChanged,
                   occurred_at    AS OccurredAt
            FROM   audit_logs
            WHERE  user_id = @UserId
            ORDER BY occurred_at DESC
            LIMIT @Limit;
            """,
            new { UserId = userId, Limit = limit },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(row => new AuditLogEntry(
            row.Id,
            row.ActionType,
            row.OccurredAt,
            row.UserId,
            row.Email,
            row.Provider,
            row.IpAddress,
            row.FieldsChanged))];
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);

    private sealed class AuditRow
    {
        public Guid Id { get; init; }
        public Guid? UserId { get; init; }
        public string? Email { get; init; }
        public string? Provider { get; init; }
        public string? IpAddress { get; init; }
        public string ActionType { get; init; } = string.Empty;
        public string? FieldsChanged { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
    }
}
