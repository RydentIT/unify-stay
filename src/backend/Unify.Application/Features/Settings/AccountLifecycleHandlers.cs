using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Features.Profile;
using Unify.Domain.Users;

namespace Unify.Application.Features.Settings;

/// <summary>
/// SET-012 / SET-013 / BR-SET-005: deactivation is reversible - a later successful login brings
/// the account back, which the login handler does. Nothing is erased here.
/// </summary>
internal sealed class DeactivateAccountCommandHandler : ICommandHandler<DeactivateAccountCommand, Result>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessions;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public DeactivateAccountCommandHandler(
        CurrentUserAccessor currentUserAccessor,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ISessionRepository sessions,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _currentUserAccessor = currentUserAccessor;
        _users = users;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(DeactivateAccountCommand request, CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        User user = resolved.Value;

        Result confirmation = AccountConfirmation.VerifyPassword(user, request.Password, _passwordHasher);

        if (confirmation.IsFailure)
        {
            return confirmation;
        }

        if (user.Status == UserStatus.Deactivated)
        {
            return Result.Failure(AuthErrors.AccountAlreadyDeactivated);
        }

        DateTimeOffset now = _clock.UtcNow;

        user.Deactivate(now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        // SET-013: the account is closed now, not when the current token happens to expire.
        await _sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.AccountDeactivated,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildAccountDeactivatedEmail(user.Email.Value, user.FullName),
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>
/// SET-012 / SET-013 / BR-SET-004.
///
/// The audit trail is written BEFORE the account is cleared, and captures the email as a value
/// rather than a reference, because the whole point of BR-SET-004 is that the history outlives
/// the account.
/// </summary>
internal sealed class DeleteAccountCommandHandler : ICommandHandler<DeleteAccountCommand, Result>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessions;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public DeleteAccountCommandHandler(
        CurrentUserAccessor currentUserAccessor,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ISessionRepository sessions,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _currentUserAccessor = currentUserAccessor;
        _users = users;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        User user = resolved.Value;

        Result confirmation = AccountConfirmation.VerifyPassword(user, request.Password, _passwordHasher);

        if (confirmation.IsFailure)
        {
            return confirmation;
        }

        string email = user.Email.Value;
        string name = user.FullName;
        DateTimeOffset now = _clock.UtcNow;

        // Send the confirmation while we still have a deliverable address.
        await _emailSender.SendAsync(
            _templates.BuildAccountDeletedEmail(email, name),
            cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.AccountDeleted,
                UserId = user.Id,
                Email = email,
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken).ConfigureAwait(false);

        user.MarkDeleted(now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        await _sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>
/// SET-012. Shared so deactivate and delete confirm identically.
///
/// A Google-only account has no password to confirm with. Rather than letting it through
/// unconfirmed, it is told plainly - the alternative would be a destructive action guarded by
/// nothing but a session cookie.
/// </summary>
internal static class AccountConfirmation
{
    public static Result VerifyPassword(User user, string password, IPasswordHasher passwordHasher)
    {
        if (!user.HasPassword)
        {
            return Result.Failure(AuthErrors.NoPasswordForGoogleAccount);
        }

        return passwordHasher.Verify(password, user.PasswordHash)
            ? Result.Success()
            : Result.Failure(AuthErrors.CurrentPasswordIncorrect);
    }
}
