using Unify.Domain.Common;

namespace Unify.Application.Abstractions.Persistence;

/// <summary>
/// Cheap round trip to Postgres used by the readiness endpoint. Returns a state rather than
/// throwing, so a down database yields a 503 report instead of an unhandled exception.
/// </summary>
public interface IDatabaseHealthProbe
{
    Task<HealthState> CheckAsync(CancellationToken cancellationToken = default);
}
