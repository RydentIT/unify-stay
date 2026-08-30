using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using Unify.Infrastructure.Migrations;
using Unify.Infrastructure.Persistence;

namespace Unify.Infrastructure.Tests.Infrastructure;

/// <summary>
/// Provides a migrated Postgres for the collection. Prefers UNIFY_TEST_POSTGRES when set
/// (CI service container), otherwise starts a throwaway Testcontainers instance.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public bool IsAvailable => !string.IsNullOrEmpty(ConnectionString);

    public async Task InitializeAsync()
    {
        if (!TestDatabaseAvailability.IsAvailable)
        {
            return;
        }

        if (TestDatabaseAvailability.ExternalConnectionStringOrNull is { } external)
        {
            ConnectionString = external;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("unify_test")
                .WithUsername("unify")
                .WithPassword("unify")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        // Apply the real database/sql/ scripts. If this throws, every test in the collection
        // fails - which is correct: a broken migration is a broken build.
        CreateMigrator().Migrate();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public DatabaseMigrator CreateMigrator() =>
        new(Options.Create(new DatabaseOptions { ConnectionString = ConnectionString }),
            NullLogger<DatabaseMigrator>.Instance);

    public IDbConnectionFactory CreateConnectionFactory() =>
        new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = ConnectionString }));

    /// <summary>Removes all rows so each test starts from a known state.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            TRUNCATE TABLE audit_logs, user_suspensions, role_upgrade_requests,
                           pending_email_changes, email_verification_tokens,
                           password_reset_tokens, sessions, login_attempts,
                           user_roles, auth_providers, users
            CASCADE;
            """,
            connection);

        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
