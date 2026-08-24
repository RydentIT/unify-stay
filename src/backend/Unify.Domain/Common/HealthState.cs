namespace Unify.Domain.Common;

/// <summary>
/// The health of a single system component, ordered from best to worst so that
/// aggregation is a simple "worst wins" fold.
/// </summary>
public enum HealthState
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2,
}

public static class HealthStateExtensions
{
    /// <summary>
    /// Folds a set of component states into the single state reported for the system.
    /// An empty set is considered healthy - nothing is broken because nothing is claimed.
    /// </summary>
    public static HealthState Aggregate(this IEnumerable<HealthState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        HealthState worst = HealthState.Healthy;

        foreach (HealthState state in states)
        {
            if (state > worst)
            {
                worst = state;
            }
        }

        return worst;
    }
}
