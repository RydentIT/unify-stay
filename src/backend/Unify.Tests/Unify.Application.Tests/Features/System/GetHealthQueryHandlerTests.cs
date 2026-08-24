using NSubstitute;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Features.System;
using Unify.Domain.Common;

namespace Unify.Application.Tests.Features.System;

public sealed class GetHealthQueryHandlerTests
{
    private readonly IDatabaseHealthProbe _probe = Substitute.For<IDatabaseHealthProbe>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private GetHealthQueryHandler CreateHandler()
    {
        _clock.UtcNow.Returns(Now);
        return new GetHealthQueryHandler(_probe, _clock);
    }

    [Fact]
    public async Task Reports_healthy_when_the_database_probe_succeeds()
    {
        _probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(HealthState.Healthy);

        HealthReport report = await CreateHandler()
            .HandleAsync(new GetHealthQuery(), CancellationToken.None);

        Assert.Equal(HealthState.Healthy, report.State);
        Assert.Equal(Now, report.CheckedAtUtc);
        Assert.Equal("database", Assert.Single(report.Components).Name);
    }

    [Theory]
    [InlineData(HealthState.Degraded)]
    [InlineData(HealthState.Unhealthy)]
    public async Task Overall_state_is_the_worst_component_state(HealthState probeState)
    {
        _probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(probeState);

        HealthReport report = await CreateHandler()
            .HandleAsync(new GetHealthQuery(), CancellationToken.None);

        Assert.Equal(probeState, report.State);
    }
}
