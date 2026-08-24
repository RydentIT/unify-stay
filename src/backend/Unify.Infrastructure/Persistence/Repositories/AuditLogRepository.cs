using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Auditing;

namespace Unify.Infrastructure.Persistence.Repositories;

/// <summary>Append-only writer for the audit_logs table. Deliberately offers no update or delete.</summary>
internal sealed class AuditLogRepository : IAuditLogRepository
{
    private const string InsertSql = """
        INSERT INTO audit_logs (
            id, action, actor_user_id, subject_type, subject_id,
            ip_address, user_agent, metadata, occurred_at_utc)
        VALUES (
            @Id, @Action, @ActorUserId, @SubjectType, @SubjectId,
            CAST(@IpAddress AS inet), @UserAgent, CAST(@Metadata AS jsonb), @OccurredAtUtc);
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                entry.Id,
                entry.Action,
                entry.ActorUserId,
                entry.SubjectType,
                entry.SubjectId,
                entry.IpAddress,
                entry.UserAgent,
                Metadata = entry.MetadataJson,
                entry.OccurredAtUtc,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
