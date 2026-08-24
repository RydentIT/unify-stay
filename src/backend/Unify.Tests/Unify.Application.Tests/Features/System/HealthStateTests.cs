using Unify.Domain.Common;

namespace Unify.Application.Tests.Features.System;

/// <summary>Covers the domain fold that decides the overall health of the system.</summary>
public sealed class HealthStateTests
{
    [Fact]
    public void An_empty_set_of_components_is_healthy()
    {
        Assert.Equal(HealthState.Healthy, Array.Empty<HealthState>().Aggregate());
    }

    [Fact]
    public void The_worst_component_state_wins()
    {
        HealthState[] states = [HealthState.Healthy, HealthState.Unhealthy, HealthState.Degraded];

        Assert.Equal(HealthState.Unhealthy, states.Aggregate());
    }

    [Fact]
    public void All_healthy_components_aggregate_to_healthy()
    {
        HealthState[] states = [HealthState.Healthy, HealthState.Healthy];

        Assert.Equal(HealthState.Healthy, states.Aggregate());
    }
}
