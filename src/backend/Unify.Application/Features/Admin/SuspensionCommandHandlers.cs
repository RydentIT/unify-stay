using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Admin;

/// <summary>
/// Part 4. Immediately revokes every active session for the target - the account is closed now,
/// not when whatever access token they are holding happens to expire (same pattern as
/// deactivate/delete in Unify.Application.Features.Settings.AccountLifecycleHandlers).
/// </summary>
internal sealed class SuspendUserCommandHandler : ICommandHandler<SuspendUserCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IUserSuspensionRepository _suspensions;
    private readonly ISessionRepository _sessions;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public SuspendUserCommandHandler(
        IUserRepository users,
        IUserSuspensionRepository suspensions,
        ISessionRepository sessions,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _users = users;
        _suspensions = suspensions;
        _sessions = sessions;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid adminId)
        {
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        User? user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure(AuthErrors.UserNotFound);
        }

        if (user.Status == UserStatus.Suspended)
        {
            return Result.Failure(AuthErrors.UserAlreadySuspended);
        }

        if (user.Status is UserStatus.Deactivated or UserStatus.Deleted)
        {
            return Result.Failure(AuthErrors.UserNotEligibleForSuspension);
        }

        DateTimeOffset now = _clock.UtcNow;

        user.Suspend(now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        UserSuspension suspension = UserSuspension.Impose(Guid.CreateVersion7(), user.Id, request.Reason, adminId, now);
        await _suspensions.AddAsync(suspension, cancellationToken).ConfigureAwait(false);

        await _sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.UserSuspended,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { request.Reason, SuspendedBy = adminId },
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Part 4. Restores login access and notifies the user; the suspension record itself
/// is marked LIFTED rather than deleted, so the history survives.</summary>
internal sealed class LiftSuspensionCommandHandler : ICommandHandler<LiftSuspensionCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IUserSuspensionRepository _suspensions;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public LiftSuspensionCommandHandler(
        IUserRepository users,
        IUserSuspensionRepository suspensions,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _users = users;
        _suspensions = suspensions;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(LiftSuspensionCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid adminId)
        {
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        User? user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure(AuthErrors.UserNotFound);
        }

        UserSuspension? suspension = await _suspensions
            .GetActiveForUserAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (suspension is null)
        {
            return Result.Failure(AuthErrors.NoActiveSuspension);
        }

        DateTimeOffset now = _clock.UtcNow;

        suspension.Lift(adminId, now);
        await _suspensions.UpdateAsync(suspension, cancellationToken).ConfigureAwait(false);

        user.LiftSuspension(now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.SuspensionLifted,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { LiftedBy = adminId },
            },
            cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildSuspensionLiftedEmail(user.Email.Value, user.FullName),
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
