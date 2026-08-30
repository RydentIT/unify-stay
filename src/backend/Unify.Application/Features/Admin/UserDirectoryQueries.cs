using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Features.Settings;

namespace Unify.Application.Features.Admin;

public sealed record UserSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<string> Roles,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record UserSummaryPageDto(
    IReadOnlyList<UserSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record SuspensionDto(
    Guid Id,
    string Reason,
    Guid SuspendedBy,
    DateTimeOffset SuspendedAt,
    string Status,
    Guid? LiftedBy,
    DateTimeOffset? LiftedAt);

/// <summary>
/// The aggregation GetUserDetailQuery assembles: profile, current suspension (if any), and
/// property-owner-request history (if any) in one call - the admin-portal detail page's one
/// round trip, per Part 3's explicit "one call, not several".
/// </summary>
public sealed record UserDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? AvatarUrl,
    string? ContactNumber,
    IReadOnlyList<string> Roles,
    string Status,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    SuspensionDto? ActiveSuspension,
    IReadOnlyList<UpgradeRequestDto> UpgradeRequests);

/// <summary>Admin-only, paginated directory listing (Part 3). Role/Status are parsed leniently -
/// an unrecognised value is a validation error, not a silently-ignored filter.</summary>
/// <param name="SortBy">"newest" (default) or "oldest", by joined date. Anything else is treated as "newest".</param>
public sealed record GetUsersQuery(
    string? Search,
    string? Role,
    string? Status,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null) : IQuery<Result<UserSummaryPageDto>>;

public sealed record GetUserDetailQuery(Guid UserId) : IQuery<Result<UserDetailDto>>;
