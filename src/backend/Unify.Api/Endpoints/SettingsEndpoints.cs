using Unify.Api.Extensions;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Features.Settings;

namespace Unify.Api.Endpoints;

/// <summary>Routes under /api/settings and the admin decision routes under /api/admin.</summary>
internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .RequireAuthorization(AuthorizationPolicies.FullAccess);

        group.MapGet("/upgrade-request", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                Result<UpgradeRequestDto?> result = await dispatcher
                    .SendAsync(new GetMyUpgradeRequestQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("GetMyUpgradeRequest")
            .WithSummary("Read the current user's latest upgrade request and its status.");

        group.MapPost("/upgrade-request", async (
                RequestPropertyOwnerUpgradeCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<UpgradeRequestDto> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .WithName("RequestPropertyOwnerUpgrade")
            .WithSummary("Submit a Property Owner upgrade request (NIC, address, second phone, property info).");

        group.MapPost("/upgrade-request/resubmit", async (
                ResubmitUpgradeRequestCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<UpgradeRequestDto> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .WithName("ResubmitUpgradeRequest")
            .WithSummary("Resubmit after a rejection.");

        group.MapPost("/deactivate", async (
                DeactivateAccountCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithName("DeactivateAccount")
            .WithSummary("Deactivate the account. Reversible by signing in again.");

        group.MapPost("/delete", async (
                DeleteAccountCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithName("DeleteAccount")
            .WithSummary("Permanently delete the account.");

        MapAdminEndpoints(app);

        return app;
    }

    private static void MapAdminEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder admin = app.MapGroup("/api/admin/upgrade-requests")
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        admin.MapGet("/", async (
                string? status,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<IReadOnlyList<UpgradeRequestDto>> result = await dispatcher
                    .SendAsync(new ListUpgradeRequestsQuery(status ?? "pending"), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("ListUpgradeRequests")
            .WithSummary("List upgrade requests by status. Defaults to pending.");

        admin.MapPost("/{requestId:guid}/approve", async (
                Guid requestId,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher
                    .SendAsync(new ApproveUpgradeRequestCommand(requestId), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("ApproveUpgradeRequest")
            .WithSummary("Approve a request and grant the Property Owner role.");

        admin.MapPost("/{requestId:guid}/reject", async (
                Guid requestId,
                RejectUpgradeRequestBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher
                    .SendAsync(new RejectUpgradeRequestCommand(requestId, body.Reason), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("RejectUpgradeRequest")
            .WithSummary("Reject a request. A reason is required.");
    }
}

/// <summary>Body for the reject route; the id comes from the path.</summary>
internal sealed record RejectUpgradeRequestBody(string Reason);
