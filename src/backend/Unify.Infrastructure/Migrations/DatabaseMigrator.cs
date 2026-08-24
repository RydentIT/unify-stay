using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unify.Infrastructure.Persistence;

namespace Unify.Infrastructure.Migrations;

/// <summary>
/// Applies the versioned scripts from database/sql/ (embedded into this assembly at build
/// time) in ordinal filename order, journalling what it has run in the schemaversions table.
/// Called at startup in Development and from scripts/migrate.* everywhere else.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Creates the database if absent, then applies every pending script.</summary>
    public void Migrate(bool ensureDatabaseExists = true)
    {
        if (ensureDatabaseExists)
        {
            EnsureDatabase.For.PostgresqlDatabase(_options.ConnectionString);
        }

        DatabaseUpgradeResult result = BuildUpgrader(_options.ConnectionString, _logger)
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database migration failed on script {result.ErrorScript?.Name ?? "(unknown)"}.",
                result.Error);
        }

        _logger.LogInformation(
            "Database is up to date. {ScriptCount} script(s) applied.",
            result.Scripts.Count());
    }

    /// <summary>Reports whether any script has not yet been applied, without applying anything.</summary>
    public bool IsUpgradeRequired() =>
        BuildUpgrader(_options.ConnectionString, _logger).IsUpgradeRequired();

    private static UpgradeEngine BuildUpgrader(string connectionString, ILogger logger) =>
        DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript()
            .LogTo(new UpgradeLogAdapter(logger))
            .Build();

    /// <summary>Bridges DbUp's logging onto the host's ILogger so migrations are not console-only.</summary>
    private sealed class UpgradeLogAdapter : IUpgradeLog
    {
        private readonly ILogger _logger;

        public UpgradeLogAdapter(ILogger logger) => _logger = logger;

        public void LogTrace(string format, params object[] args) =>
            _logger.LogTrace("DbUp: " + format, args);

        public void LogDebug(string format, params object[] args) =>
            _logger.LogDebug("DbUp: " + format, args);

        public void LogInformation(string format, params object[] args) =>
            _logger.LogInformation("DbUp: " + format, args);

        public void LogWarning(string format, params object[] args) =>
            _logger.LogWarning("DbUp: " + format, args);

        public void LogError(string format, params object[] args) =>
            _logger.LogError("DbUp: " + format, args);

        public void LogError(Exception ex, string format, params object[] args) =>
            _logger.LogError(ex, "DbUp: " + format, args);
    }
}
