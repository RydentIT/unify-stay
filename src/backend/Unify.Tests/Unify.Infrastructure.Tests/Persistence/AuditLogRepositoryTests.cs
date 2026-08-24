using Dapper;
using Unify.Domain.Auditing;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Tests.Infrastructure;

namespace Unify.Infrastructure.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class AuditLogRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public AuditLogRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    [RequiresPostgresFact]
    public async Task Appends_an_entry_including_its_jsonb_metadata_and_inet_address()
    {
        await _postgres.ResetAsync();

        var repository = new AuditLogRepository(_postgres.CreateConnectionFactory());
        Guid id = Guid.CreateVersion7();

        await repository.AppendAsync(new AuditLogEntry(
            id,
            action: "user.register.attempted",
            actorUserId: null,
            subjectType: "user",
            subjectId: "someone@example.com",
            ipAddress: "203.0.113.7",
            userAgent: "unify-tests/1.0",
            metadataJson: """{"source":"integration-test"}""",
            occurredAtUtc: DateTimeOffset.UtcNow));

        await using var connection = await _postgres.CreateConnectionFactory().OpenConnectionAsync();

        var stored = await connection.QuerySingleAsync<(string Action, string Ip, string Metadata)>(
            """
            SELECT action                 AS "Action",
                   host(ip_address)       AS "Ip",
                   metadata::text         AS "Metadata"
            FROM   audit_logs
            WHERE  id = @Id;
            """,
            new { Id = id });

        Assert.Equal("user.register.attempted", stored.Action);
        Assert.Equal("203.0.113.7", stored.Ip);
        Assert.Contains("integration-test", stored.Metadata, StringComparison.Ordinal);
    }

    [RequiresPostgresFact]
    public async Task Accepts_an_anonymous_actor_and_null_metadata()
    {
        await _postgres.ResetAsync();

        var repository = new AuditLogRepository(_postgres.CreateConnectionFactory());
        Guid id = Guid.CreateVersion7();

        await repository.AppendAsync(new AuditLogEntry(
            id,
            action: "user.login.failed",
            actorUserId: null,
            subjectType: null,
            subjectId: null,
            ipAddress: null,
            userAgent: null,
            metadataJson: null,
            occurredAtUtc: DateTimeOffset.UtcNow));

        await using var connection = await _postgres.CreateConnectionFactory().OpenConnectionAsync();

        int count = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM audit_logs WHERE id = @Id;",
            new { Id = id });

        Assert.Equal(1, count);
    }
}
