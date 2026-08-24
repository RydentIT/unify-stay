using Dapper;
using Unify.Infrastructure.Tests.Infrastructure;

namespace Unify.Infrastructure.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class MigrationTests
{
    private readonly PostgresFixture _postgres;

    public MigrationTests(PostgresFixture postgres) => _postgres = postgres;

    [RequiresPostgresFact]
    public async Task Every_expected_table_exists_after_migration()
    {
        await using var connection = await _postgres.CreateConnectionFactory().OpenConnectionAsync();

        IEnumerable<string> tables = await connection.QueryAsync<string>(
            """
            SELECT table_name
            FROM   information_schema.tables
            WHERE  table_schema = 'public';
            """);

        var actual = tables.ToHashSet(StringComparer.Ordinal);

        Assert.Contains("users", actual);
        Assert.Contains("auth_providers", actual);
        Assert.Contains("user_roles", actual);
        Assert.Contains("audit_logs", actual);

        // DbUp's journal, which is what makes a second run a no-op.
        Assert.Contains("schemaversions", actual);
    }

    [RequiresPostgresFact]
    public void Running_the_migrator_again_applies_nothing()
    {
        // The fixture already migrated. Re-running must be a no-op, which is what lets the
        // API migrate on startup in Development without special-casing a fresh database.
        Assert.False(_postgres.CreateMigrator().IsUpgradeRequired());

        _postgres.CreateMigrator().Migrate();

        Assert.False(_postgres.CreateMigrator().IsUpgradeRequired());
    }

    [RequiresPostgresFact]
    public async Task The_unique_email_index_is_present()
    {
        await using var connection = await _postgres.CreateConnectionFactory().OpenConnectionAsync();

        bool exists = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'ux_users_email');
            """);

        Assert.True(exists);
    }
}
