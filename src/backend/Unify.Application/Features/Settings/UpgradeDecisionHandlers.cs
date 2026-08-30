using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Settings;
using Unify.Domain.Users;

namespace Unify.Application.Features.Settings;

/// <summary>
/// Admin listing of upgrade requests. Authorization is enforced by the endpoint's admin-only
/// policy; this handler assumes an admin has already been established.
/// </summary>
internal sealed class ListUpgradeRequestsQueryHandler
    : IQueryHandler<ListUpgradeRequestsQuery, Result<IReadOnlyList<UpgradeRequestDto>>>
{
    private readonly IRoleUpgradeRequestRepository _requests;
    private readonly IUserRepository _users;

    public ListUpgradeRequestsQueryHandler(IRoleUpgradeRequestRepository requests, IUserRepository users)
    {
        _requests = requests;
        _users = users;
    }

    public async Task<Result<IReadOnlyList<UpgradeRequestDto>>> HandleAsync(
        ListUpgradeRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Status, ignoreCase: true, out UpgradeRequestStatus status))
        {
            status = UpgradeRequestStatus.Pending;
        }

        IReadOnlyList<RoleUpgradeRequest> found = await _requests
            .ListByStatusAsync(status, cancellationToken)
            .ConfigureAwait(false);

        var results = new List<UpgradeRequestDto>(found.Count);

        foreach (RoleUpgradeRequest upgradeRequest in found)
        {
            User? applicant = await _users
                .GetByIdAsync(upgradeRequest.UserId, cancellationToken)
                .ConfigureAwait(false);

            results.Add(UpgradeRequestMapper.ToDto(upgradeRequest, applicant));
        }

        return Result.Success<IReadOnlyList<UpgradeRequestDto>>(results);
    }
}

/// <summary>SET-009 / BR-SET-002: approval grants the Property Owner role immediately.</summary>
internal sealed class ApproveUpgradeRequestCommandHandler : ICommandHandler<ApproveUpgradeRequestCommand, Result>
{
    private readonly IRoleUpgradeRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ApproveUpgradeRequestCommandHandler(
        IRoleUpgradeRequestRepository requests,
        IUserRepository users,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _requests = requests;
        _users = users;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(ApproveUpgradeRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid adminId)
        {
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        RoleUpgradeRequest? upgradeRequest = await _requests
            .GetByIdAsync(request.RequestId, cancellationToken)
            .ConfigureAwait(false);

        if (upgradeRequest is null)
        {
            return Result.Failure(AuthErrors.UpgradeRequestNotFound);
        }

        if (!upgradeRequest.IsPending)
        {
            return Result.Failure(AuthErrors.UpgradeRequestAlreadyDecided);
        }

        User? applicant = await _users
            .GetByIdAsync(upgradeRequest.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (applicant is null || applicant.IsDeleted)
        {
            return Result.Failure(AuthErrors.UserNotFound);
        }

        DateTimeOffset now = _clock.UtcNow;

        upgradeRequest.Approve(adminId, now);
        await _requests.UpdateAsync(upgradeRequest, cancellationToken).ConfigureAwait(false);

        // The role is granted here rather than left for a later job: BR-SET-002 says approval
        // takes effect immediately. Student is retained - the upgrade adds a capability.
        await _users.AddRoleAsync(applicant.Id, RoleName.PropertyOwner, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.UpgradeApproved,
                UserId = applicant.Id,
                Email = applicant.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { requestId = upgradeRequest.Id, decidedBy = adminId, roleGranted = nameof(RoleName.PropertyOwner) },
            },
            cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildUpgradeApprovedEmail(applicant.Email.Value, applicant.FullName),
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>SET-007: rejection requires a reason, and that reason reaches the applicant.</summary>
internal sealed class RejectUpgradeRequestCommandHandler : ICommandHandler<RejectUpgradeRequestCommand, Result>
{
    private readonly IRoleUpgradeRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public RejectUpgradeRequestCommandHandler(
        IRoleUpgradeRequestRepository requests,
        IUserRepository users,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _requests = requests;
        _users = users;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(RejectUpgradeRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid adminId)
        {
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        RoleUpgradeRequest? upgradeRequest = await _requests
            .GetByIdAsync(request.RequestId, cancellationToken)
            .ConfigureAwait(false);

        if (upgradeRequest is null)
        {
            return Result.Failure(AuthErrors.UpgradeRequestNotFound);
        }

        if (!upgradeRequest.IsPending)
        {
            return Result.Failure(AuthErrors.UpgradeRequestAlreadyDecided);
        }

        User? applicant = await _users
            .GetByIdAsync(upgradeRequest.UserId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = _clock.UtcNow;

        // The domain refuses a rejection without a reason; the validator has already required it.
        upgradeRequest.Reject(adminId, request.Reason, now);
        await _requests.UpdateAsync(upgradeRequest, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.UpgradeRejected,
                UserId = upgradeRequest.UserId,
                Email = applicant?.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { requestId = upgradeRequest.Id, decidedBy = adminId, reason = request.Reason },
            },
            cancellationToken).ConfigureAwait(false);

        if (applicant is not null)
        {
            await _emailSender.SendAsync(
                _templates.BuildUpgradeRejectedEmail(applicant.Email.Value, applicant.FullName, request.Reason),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
