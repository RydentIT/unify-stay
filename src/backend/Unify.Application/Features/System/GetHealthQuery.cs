using Unify.Application.Abstractions.Messaging;
using Unify.Domain.Common;

namespace Unify.Application.Features.System;

public sealed record ComponentHealth(string Name, HealthState State);

public sealed record HealthReport(
    HealthState State,
    IReadOnlyCollection<ComponentHealth> Components,
    DateTimeOffset CheckedAtUtc);

/// <summary>
/// Readiness check. This is the scaffold's end-to-end proof point: the API endpoint hands it
/// to the dispatcher (Application), the handler calls the database probe port implemented
/// with Dapper (Infrastructure), and the component states are folded by a Domain rule.
/// </summary>
public sealed record GetHealthQuery : IQuery<HealthReport>;
