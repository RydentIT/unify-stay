using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

/// <summary>
/// BR-PRF-005: only name, avatar and contact number are writable here. The command carries no
/// role or verification fields at all, so there is nothing for a crafted request to smuggle in.
/// </summary>
internal sealed class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IUserRepository _users;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public UpdateProfileCommandHandler(
        CurrentUserAccessor currentUserAccessor,
        IUserRepository users,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _currentUserAccessor = currentUserAccessor;
        _users = users;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ProfileDto>> HandleAsync(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure<ProfileDto>(resolved.Error);
        }

        User user = resolved.Value;

        // JSON binding can hand us a null despite the non-nullable record property; the
        // validator rejects it, but the flow analysis cannot see that from here.
        string newFirstName = (request.FirstName ?? string.Empty).Trim();
        string newLastName = (request.LastName ?? string.Empty).Trim();

        // Captured before mutation so the audit entry can name what actually changed.
        var changed = new List<string>();

        if (!string.Equals(user.FirstName, newFirstName, StringComparison.Ordinal))
        {
            changed.Add(nameof(user.FirstName));
        }

        if (!string.Equals(user.LastName, newLastName, StringComparison.Ordinal))
        {
            changed.Add(nameof(user.LastName));
        }

        if (!string.Equals(user.AvatarUrl, request.AvatarUrl, StringComparison.Ordinal))
        {
            changed.Add(nameof(user.AvatarUrl));
        }

        if (!string.Equals(user.ContactNumber, request.ContactNumber, StringComparison.Ordinal))
        {
            changed.Add(nameof(user.ContactNumber));
        }

        user.UpdateProfile(newFirstName, newLastName, request.AvatarUrl, request.ContactNumber, _clock.UtcNow);

        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.ProfileUpdated,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { fields = changed },
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(ProfileMapper.ToDto(user));
    }
}
