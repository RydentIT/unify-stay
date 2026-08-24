using Unify.Application.Abstractions.Messaging;
using Unify.Application.Features.System;
using Unify.Domain.Common;

namespace Unify.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/health")
            .WithTags("Health")
            .AllowAnonymous();

        // Liveness: answers as long as the process is up. Deliberately touches no dependency,
        // so an orchestrator never restarts a healthy API because the database blinked.
        group.MapGet("/live", () => Results.Ok(new { status = "alive" }))
            .WithName("HealthLive")
            .WithSummary("Liveness probe. Never touches a dependency.");

        // Readiness: the scaffold's end-to-end proof point. This request travels API ->
        // dispatcher (Application) -> Dapper probe (Infrastructure) and folds the component
        // states with a Domain rule, so a 200 here means all four layers are wired correctly.
        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                HealthReport report = await dispatcher
                    .SendAsync(new GetHealthQuery(), cancellationToken)
                    .ConfigureAwait(false);

                var body = new
                {
                    status = report.State.ToString(),
                    checkedAtUtc = report.CheckedAtUtc,
                    components = report.Components.ToDictionary(
                        component => component.Name,
                        component => component.State.ToString(),
                        StringComparer.Ordinal),
                };

                return report.State == HealthState.Unhealthy
                    ? Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable)
                    : Results.Ok(body);
            })
            .WithName("HealthReady")
            .WithSummary("Readiness probe. Round trips through every backend layer.");

        return app;
    }
}
