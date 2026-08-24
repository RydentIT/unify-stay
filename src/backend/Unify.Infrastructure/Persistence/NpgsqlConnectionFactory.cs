using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Unify.Infrastructure.Persistence;

/// <summary>
/// Wraps a single <see cref="NpgsqlDataSource"/>, which owns the connection pool. The data
/// source is registered as a singleton and disposed with the host.
/// </summary>
internal sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dataSource = new NpgsqlDataSourceBuilder(options.Value.ConnectionString).Build();
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
