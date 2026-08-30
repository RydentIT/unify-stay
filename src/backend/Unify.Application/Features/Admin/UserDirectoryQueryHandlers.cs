using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Common;
using Unify.Application.Features.Settings;
using Unify.Domain.Settings;
using Unify.Domain.Users;

namespace Unify.Application.Features.Admin;

internal sealed class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, Result<UserSummaryPageDto>>
{
    private readonly IUserRepository _users;

    public GetUsersQueryHandler(IUserRepository users) => _users = users;

    public async Task<Result<UserSummaryPageDto>> HandleAsync(GetUsersQuery request, CancellationToken cancellationToken)
    {
        RoleName? role = null;

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse(request.Role, ignoreCase: true, out RoleName parsedRole))
            {
                return Result.Failure<UserSummaryPageDto>(AuthErrors.InvalidRoleFilter);
            }

            role = parsedRole;
        }

        UserStatus? status = null;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse(request.Status, ignoreCase: true, out UserStatus parsedStatus))
            {
                return Result.Failure<UserSummaryPageDto>(AuthErrors.InvalidStatusFilter);
            }

            status = parsedStatus;
        }

        var filter = new UserDirectoryFilter(
            request.Search,
            role,
            status,
            request.Page,
            request.PageSize,
            OldestFirst: string.Equals(request.SortBy, "oldest", StringComparison.OrdinalIgnoreCase));

        UserDirectoryPage page = await _users.SearchDirectoryAsync(filter, cancellationToken).ConfigureAwait(false);

        UserSummaryDto[] items = [.. page.Items.Select(entry => new UserSummaryDto(
            entry.Id,
            entry.FirstName,
            entry.LastName,
            entry.Email,
            [.. entry.Roles.Select(r => r.ToString())],
            entry.Status.ToString(),
            entry.CreatedAt))];

        return Result.Success(new UserSummaryPageDto(items, page.TotalCount, page.Page, page.PageSize));
    }
}

/// <summary>
/// Aggregates profile + current suspension + upgrade-request history in one call (Part 3), so
/// the admin-portal detail page needs exactly one round trip.
/// </summary>
internal sealed class GetUserDetailQueryHandler : IQueryHandler<GetUserDetailQuery, Result<UserDetailDto>>
{
    private readonly IUserRepository _users;
    private readonly IUserSuspensionRepository _suspensions;
    private readonly IRoleUpgradeRequestRepository _upgradeRequests;

    public GetUserDetailQueryHandler(
        IUserRepository users,
        IUserSuspensionRepository suspensions,
        IRoleUpgradeRequestRepository upgradeRequests)
    {
        _users = users;
        _suspensions = suspensions;
        _upgradeRequests = upgradeRequests;
    }

    public async Task<Result<UserDetailDto>> HandleAsync(GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure<UserDetailDto>(AuthErrors.UserNotFound);
        }

        UserSuspension? activeSuspension = await _suspensions
            .GetActiveForUserAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<RoleUpgradeRequest> upgradeRequests = await _upgradeRequests
            .ListByUserAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        SuspensionDto? suspensionDto = activeSuspension is null
            ? null
            : new SuspensionDto(
                activeSuspension.Id,
                activeSuspension.Reason,
                activeSuspension.SuspendedBy,
                activeSuspension.SuspendedAt,
                activeSuspension.Status.ToString(),
                activeSuspension.LiftedBy,
                activeSuspension.LiftedAt);

        var dto = new UserDetailDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.AvatarUrl,
            user.ContactNumber,
            [.. user.Roles.Select(r => r.ToString())],
            user.Status.ToString(),
            user.EmailVerified,
            user.CreatedAt,
            suspensionDto,
            [.. upgradeRequests.Select(r => UpgradeRequestMapper.ToDto(r, user))]);

        return Result.Success(dto);
    }
}
