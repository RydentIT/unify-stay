using System.Data.Common;

namespace Unify.Infrastructure.Persistence;

/// <summary>
/// Hands out open Npgsql connections. Infrastructure-internal on purpose: Unify.Application
/// must not know that persistence is a relational database at all.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
