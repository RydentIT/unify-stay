using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Domain.Common;

namespace Unify.Application.Features.System;

internal sealed class GetHealthQueryHandler : IQueryHandler<GetHealthQuery, HealthReport>
{
    private readonly IDatabaseHealthProbe _databaseHealthProbe;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetHealthQueryHandler(
        IDatabaseHealthProbe databaseHealthProbe,
        IDateTimeProvider dateTimeProvider)
    {
        _databaseHealthProbe = databaseHealthProbe;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<HealthReport> HandleAsync(
        GetHealthQuery request,
        CancellationToken cancellationToken)
    {
        HealthState database = await _databaseHealthProbe
            .CheckAsync(cancellationToken)
            .ConfigureAwait(false);

        ComponentHealth[] components = [new ComponentHealth("database", database)];

        return new HealthReport(
            components.Select(component => component.State).Aggregate(),
            components,
            _dateTimeProvider.UtcNow);
    }
}
