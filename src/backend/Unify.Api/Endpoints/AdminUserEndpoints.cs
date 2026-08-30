using Unify.Api.Extensions;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Features.Admin;

namespace Unify.Api.Endpoints;

/// <summary>
/// Part 3 / Part 4: the admin user directory and the suspend/lift-suspension actions that hang
/// off its detail view. GET suspension detail is deliberately folded into GET
/// /api/admin/users/{userId} rather than exposed separately, so the admin-portal detail page
/// needs one call, not several.
/// </summary>
internal static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async (
                string? search,
                string? role,
                string? status,
                int? page,
                int? pageSize,
                string? sortBy,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<UserSummaryPageDto> result = await dispatcher
                    .SendAsync(
                        new GetUsersQuery(search, role, status, page ?? 1, pageSize ?? 20, sortBy),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("GetUsers")
            .WithSummary("Paginated, searchable, filterable directory of all users.");

        group.MapGet("/{userId:guid}", async (
                Guid userId,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<UserDetailDto> result = await dispatcher
                    .SendAsync(new GetUserDetailQuery(userId), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("GetUserDetail")
            .WithSummary("Full profile, current suspension (if any), and upgrade-request history for one user.");

        group.MapPost("/{userId:guid}/suspend", async (
                Guid userId,
                SuspendUserBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher
                    .SendAsync(new SuspendUserCommand(userId, body.Reason), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("SuspendUser")
            .WithSummary("Suspend (ban) a user. A reason is required. Revokes all of their active sessions.");

        group.MapPost("/{userId:guid}/lift-suspension", async (
                Guid userId,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher
                    .SendAsync(new LiftSuspensionCommand(userId), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("LiftSuspension")
            .WithSummary("Lift an active suspension and restore login access.");

        return app;
    }
}

/// <summary>Body for the suspend route; the id comes from the path.</summary>
internal sealed record SuspendUserBody(string Reason);
