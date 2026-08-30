using NSubstitute;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Common;
using Unify.Application.Features.Admin;
using Unify.Application.Features.Settings;
using Unify.Domain.Settings;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Admin;

/// <summary>
/// Part 3: the admin user directory. Filtering/pagination correctness against real SQL is an
/// Infrastructure-level concern (RequiresPostgresFact); these tests cover the handler's own
/// job - translating query-string filters into the repository call, and assembling the detail
/// aggregation from its three sources.
/// </summary>
public sealed class UserDirectoryTests
{
    private readonly TestKit _kit = new();

    private GetUsersQueryHandler CreateUsersHandler() => new(_kit.Users);

    private GetUserDetailQueryHandler CreateDetailHandler() => new(_kit.Users, _kit.Suspensions, _kit.UpgradeRequests);

    [Fact]
    public async Task Rejects_an_unrecognised_role_filter()
    {
        Result<UserSummaryPageDto> result = await CreateUsersHandler().HandleAsync(
            new GetUsersQuery(Search: null, Role: "not-a-role", Status: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidRoleFilter, result.Error);
    }

    [Fact]
    public async Task Rejects_an_unrecognised_status_filter()
    {
        Result<UserSummaryPageDto> result = await CreateUsersHandler().HandleAsync(
            new GetUsersQuery(Search: null, Role: null, Status: "not-a-status"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidStatusFilter, result.Error);
    }

    [Fact]
    public async Task Translates_search_role_and_status_into_the_repository_filter()
    {
        _kit.Users
            .SearchDirectoryAsync(Arg.Any<UserDirectoryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryPage([], TotalCount: 0, Page: 2, PageSize: 10));

        await CreateUsersHandler().HandleAsync(
            new GetUsersQuery("ann", "Student", "active", Page: 2, PageSize: 10, SortBy: "oldest"),
            CancellationToken.None);

        await _kit.Users.Received(1).SearchDirectoryAsync(
            Arg.Is<UserDirectoryFilter>(filter =>
                filter.Search == "ann" &&
                filter.Role == RoleName.Student &&
                filter.Status == UserStatus.Active &&
                filter.Page == 2 &&
                filter.PageSize == 10 &&
                filter.OldestFirst),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Maps_the_repository_page_into_the_response_including_total_count()
    {
        var entry = new UserDirectoryEntry(
            Guid.CreateVersion7(), "Ann", "Owner", "ann@example.com",
            [RoleName.PropertyOwner], UserStatus.Active, TestKit.Now.AddDays(-5));

        _kit.Users
            .SearchDirectoryAsync(Arg.Any<UserDirectoryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryPage([entry], TotalCount: 37, Page: 1, PageSize: 20));

        Result<UserSummaryPageDto> result = await CreateUsersHandler().HandleAsync(
            new GetUsersQuery(null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(37, result.Value.TotalCount);
        Assert.Single(result.Value.Items);
        Assert.Equal("ann@example.com", result.Value.Items[0].Email);
        Assert.Contains(nameof(RoleName.PropertyOwner), result.Value.Items[0].Roles);
    }

    [Fact]
    public async Task Detail_query_reports_not_found_for_an_unknown_user()
    {
        _kit.Users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        Result<UserDetailDto> result = await CreateDetailHandler().HandleAsync(
            new GetUserDetailQuery(Guid.CreateVersion7()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserNotFound, result.Error);
    }

    /// <summary>The one call, not several: profile + suspension + upgrade history together.</summary>
    [Fact]
    public async Task Detail_query_aggregates_profile_suspension_and_upgrade_history_in_one_call()
    {
        User user = TestKit.ActiveStudent(status: UserStatus.Suspended);
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Guid adminId = Guid.CreateVersion7();
        UserSuspension suspension = UserSuspension.Impose(
            Guid.CreateVersion7(), user.Id, "Repeated violations", adminId, TestKit.Now);
        _kit.Suspensions.GetActiveForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(suspension);

        RoleUpgradeRequest upgradeRequest = RoleUpgradeRequest.Submit(
            Guid.CreateVersion7(), user.Id, "199912345678", "123 Galle Road", "+94770000000",
            "Two-bedroom apartment.", TestKit.Now.AddDays(-2));
        _kit.UpgradeRequests
            .ListByUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<RoleUpgradeRequest>)[upgradeRequest]);

        Result<UserDetailDto> result = await CreateDetailHandler().HandleAsync(
            new GetUserDetailQuery(user.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        UserDetailDto dto = result.Value;

        Assert.Equal(user.Id, dto.Id);
        Assert.NotNull(dto.ActiveSuspension);
        Assert.Equal("Repeated violations", dto.ActiveSuspension!.Reason);
        Assert.Equal(adminId, dto.ActiveSuspension.SuspendedBy);
        Assert.Single(dto.UpgradeRequests);
        Assert.Equal("199912345678", dto.UpgradeRequests[0].NicNumber);
    }

    [Fact]
    public async Task Detail_query_reports_no_active_suspension_when_the_user_was_never_suspended()
    {
        User user = TestKit.ActiveStudent();
        _kit.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _kit.Suspensions.GetActiveForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns((UserSuspension?)null);
        _kit.UpgradeRequests
            .ListByUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<RoleUpgradeRequest>)[]);

        Result<UserDetailDto> result = await CreateDetailHandler().HandleAsync(
            new GetUserDetailQuery(user.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ActiveSuspension);
        Assert.Empty(result.Value.UpgradeRequests);
    }
}
