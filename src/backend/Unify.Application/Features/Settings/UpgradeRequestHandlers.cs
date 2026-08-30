using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Features.Profile;
using Unify.Domain.Settings;
using Unify.Domain.Users;

namespace Unify.Application.Features.Settings;

/// <summary>
/// Submitting an upgrade request (SET-005, BR-SET-001).
///
/// The user keeps their existing role and access the whole time it is pending - nothing here
/// touches roles. Only approval grants PropertyOwner.
/// </summary>
internal sealed class RequestPropertyOwnerUpgradeCommandHandler
    : ICommandHandler<RequestPropertyOwnerUpgradeCommand, Result<UpgradeRequestDto>>
{
    private readonly UpgradeRequestService _service;

    public RequestPropertyOwnerUpgradeCommandHandler(UpgradeRequestService service) => _service = service;

    public Task<Result<UpgradeRequestDto>> HandleAsync(
        RequestPropertyOwnerUpgradeCommand request,
        CancellationToken cancellationToken) =>
        _service.SubmitAsync(
            request.NicNumber,
            request.Address,
            request.PhoneNumber2,
            request.PropertyInfo,
            isResubmission: false,
            cancellationToken);
}

/// <summary>SET-008: resubmission, permitted only after a rejection.</summary>
internal sealed class ResubmitUpgradeRequestCommandHandler
    : ICommandHandler<ResubmitUpgradeRequestCommand, Result<UpgradeRequestDto>>
{
    private readonly UpgradeRequestService _service;

    public ResubmitUpgradeRequestCommandHandler(UpgradeRequestService service) => _service = service;

    public Task<Result<UpgradeRequestDto>> HandleAsync(
        ResubmitUpgradeRequestCommand request,
        CancellationToken cancellationToken) =>
        _service.SubmitAsync(
            request.NicNumber,
            request.Address,
            request.PhoneNumber2,
            request.PropertyInfo,
            isResubmission: true,
            cancellationToken);
}

internal sealed class GetMyUpgradeRequestQueryHandler
    : IQueryHandler<GetMyUpgradeRequestQuery, Result<UpgradeRequestDto?>>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IRoleUpgradeRequestRepository _requests;

    public GetMyUpgradeRequestQueryHandler(
        CurrentUserAccessor currentUserAccessor,
        IRoleUpgradeRequestRepository requests)
    {
        _currentUserAccessor = currentUserAccessor;
        _requests = requests;
    }

    public async Task<Result<UpgradeRequestDto?>> HandleAsync(
        GetMyUpgradeRequestQuery request,
        CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure<UpgradeRequestDto?>(resolved.Error);
        }

        // Latest rather than active, so the UI can show a rejection and its reason (SET-007).
        RoleUpgradeRequest? latest = await _requests
            .GetLatestForUserAsync(resolved.Value.Id, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(latest is null ? null : UpgradeRequestMapper.ToDto(latest, resolved.Value));
    }
}

/// <summary>
/// Shared submission path for the first request and a resubmission. Kept as one service because
/// the two differ only in which prior state they will accept.
/// </summary>
internal sealed class UpgradeRequestService
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IRoleUpgradeRequestRepository _requests;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public UpgradeRequestService(
        CurrentUserAccessor currentUserAccessor,
        IRoleUpgradeRequestRepository requests,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _currentUserAccessor = currentUserAccessor;
        _requests = requests;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<UpgradeRequestDto>> SubmitAsync(
        string nicNumber,
        string address,
        string phoneNumber2,
        string propertyInfo,
        bool isResubmission,
        CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure<UpgradeRequestDto>(resolved.Error);
        }

        User user = resolved.Value;

        // Phone 1 is the account's existing contact number from registration; only Phone 2 is
        // collected here, and it has to be a genuinely different number to be worth asking for.
        if (string.Equals(
                NormalisePhone(phoneNumber2),
                NormalisePhone(user.ContactNumber),
                StringComparison.Ordinal))
        {
            return Result.Failure<UpgradeRequestDto>(AuthErrors.SecondPhoneMatchesPrimary);
        }

        // BR-SET-001: a pending decision or an already-granted upgrade both block a new one.
        RoleUpgradeRequest? active = await _requests
            .GetActiveForUserAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (active is not null)
        {
            return Result.Failure<UpgradeRequestDto>(AuthErrors.UpgradeRequestAlreadyActive);
        }

        if (isResubmission)
        {
            // SET-008: there must be a rejection to resubmit against. Without this check the
            // resubmit route would be an alias for submit.
            RoleUpgradeRequest? latest = await _requests
                .GetLatestForUserAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            if (latest is null || latest.Status != UpgradeRequestStatus.Rejected)
            {
                return Result.Failure<UpgradeRequestDto>(AuthErrors.UpgradeRequestNotRejected);
            }
        }

        DateTimeOffset now = _clock.UtcNow;

        var upgradeRequest = RoleUpgradeRequest.Submit(
            Guid.CreateVersion7(),
            user.Id,
            nicNumber,
            address,
            phoneNumber2,
            propertyInfo,
            now);

        await _requests.AddAsync(upgradeRequest, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = isResubmission ? AuditActions.UpgradeResubmitted : AuditActions.UpgradeRequested,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { requestId = upgradeRequest.Id },
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(UpgradeRequestMapper.ToDto(upgradeRequest, user));
    }

    /// <summary>Strips everything but digits so "+94 71 234 5678" and "0712345678"-style
    /// variants of the same number compare equal rather than differing on formatting alone.</summary>
    private static string NormalisePhone(string? phone) =>
        phone is null ? string.Empty : new string([.. phone.Where(char.IsDigit)]);
}

internal static class UpgradeRequestMapper
{
    public static UpgradeRequestDto ToDto(RoleUpgradeRequest request, User? user) => new(
        request.Id,
        request.UserId,
        user?.FullName,
        user?.Email.Value,
        request.NicNumber,
        request.Address,
        request.PhoneNumber2,
        request.PropertyInfo,
        request.Status.ToString(),
        request.RejectionReason,
        request.SubmittedAt,
        request.DecidedAt);
}
