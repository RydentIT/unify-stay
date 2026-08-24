using Dapper;
using Microsoft.Extensions.Logging;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Common;

namespace Unify.Infrastructure.Persistence;

/// <summary>
/// Cheapest possible round trip to Postgres. Reports Unhealthy rather than throwing so the
/// readiness endpoint can answer 503 with a body instead of blowing up in middleware.
/// </summary>
internal sealed class PostgresDatabaseHealthProbe : IDatabaseHealthProbe
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PostgresDatabaseHealthProbe> _logger;

    public PostgresDatabaseHealthProbe(
        IDbConnectionFactory connectionFactory,
        ILogger<PostgresDatabaseHealthProbe> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<HealthState> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

            var command = new CommandDefinition(
                "SELECT 1;",
                cancellationToken: cancellationToken);

            _ = await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);

            return HealthState.Healthy;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Database health probe failed.");
            return HealthState.Unhealthy;
        }
    }
}
