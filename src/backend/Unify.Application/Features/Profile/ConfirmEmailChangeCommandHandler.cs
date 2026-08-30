using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

/// <summary>
/// Promotes the pending address to the account's real one.
///
/// Deliberately does NOT go through CurrentUserAccessor: the user follows this link from their
/// new inbox, quite possibly in a browser with no session at all. The token is the credential.
/// </summary>
internal sealed class ConfirmEmailChangeCommandHandler : ICommandHandler<ConfirmEmailChangeCommand, Result>
{
    private readonly IPendingEmailChangeRepository _pendingChanges;
    private readonly IUserRepository _users;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ConfirmEmailChangeCommandHandler(
        IPendingEmailChangeRepository pendingChanges,
        IUserRepository users,
        ISecureTokenGenerator tokenGenerator,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _pendingChanges = pendingChanges;
        _users = users;
        _tokenGenerator = tokenGenerator;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(ConfirmEmailChangeCommand request, CancellationToken cancellationToken)
    {
        string hash = _tokenGenerator.Hash(request.Token);

        PendingEmailChange? pending = await _pendingChanges
            .GetByHashAsync(hash, cancellationToken)
            .ConfigureAwait(false);

        if (pending is null)
        {
            return Result.Failure(AuthErrors.InvalidVerificationToken);
        }

        DateTimeOffset now = _clock.UtcNow;

        if (pending.IsUsed)
        {
            return Result.Failure(AuthErrors.UsedVerificationToken);
        }

        if (pending.IsExpired(now))
        {
            return Result.Failure(AuthErrors.ExpiredVerificationToken);
        }

        User? user = await _users.GetByIdAsync(pending.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return Result.Failure(AuthErrors.UserNotFound);
        }

        // Re-check at confirmation time: the address may have been claimed by someone else in
        // the window between requesting and confirming.
        if (await _users.ExistsByEmailAsync(pending.NewEmail, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(AuthErrors.EmailChangeAddressInUse);
        }

        string previousEmail = user.Email.Value;

        user.CompleteEmailChange(pending.NewEmail, now);

        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _pendingChanges.MarkUsedAsync(pending.Id, now, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.EmailChangeCompleted,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { previousEmail, newEmail = user.Email.Value },
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
