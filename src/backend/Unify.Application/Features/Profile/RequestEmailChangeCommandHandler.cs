using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

/// <summary>
/// PRF-009 / BR-PRF-002: the new address is parked in pending_email_changes and the account
/// keeps using the old one until the new address proves itself. A typo therefore costs nothing
/// - the user simply never confirms, and the change lapses.
///
/// Two emails go out: the confirmation link to the NEW address, and a notice to the OLD one so
/// an attacker with a live session cannot quietly move the account to their own inbox.
/// </summary>
internal sealed class RequestEmailChangeCommandHandler : ICommandHandler<RequestEmailChangeCommand, Result>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly IUserRepository _users;
    private readonly IPendingEmailChangeRepository _pendingChanges;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly AuthOptions _auth;
    private readonly AppUrlOptions _urls;

    public RequestEmailChangeCommandHandler(
        CurrentUserAccessor currentUserAccessor,
        IUserRepository users,
        IPendingEmailChangeRepository pendingChanges,
        IPasswordHasher passwordHasher,
        ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        IOptions<AuthOptions> auth,
        IOptions<AppUrlOptions> urls)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(urls);

        _currentUserAccessor = currentUserAccessor;
        _users = users;
        _pendingChanges = pendingChanges;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
        _auth = auth.Value;
        _urls = urls.Value;
    }

    public async Task<Result> HandleAsync(RequestEmailChangeCommand request, CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        User user = resolved.Value;

        // Changing the address is a takeover-grade action, so it is gated on the password the
        // same way a password change is. Google-only accounts cannot use this route.
        if (!user.HasPassword)
        {
            return Result.Failure(AuthErrors.NoPasswordForGoogleAccount);
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(AuthErrors.CurrentPasswordIncorrect);
        }

        var newEmail = Email.Create(request.NewEmail);

        if (newEmail.Equals(user.Email))
        {
            return Result.Failure(AuthErrors.EmailChangeAddressInUse);
        }

        // Taken by another account, or already the target of another pending change.
        bool takenByAccount = await _users.ExistsByEmailAsync(newEmail, cancellationToken).ConfigureAwait(false);
        bool takenByPending = await _pendingChanges.IsEmailTakenAsync(newEmail, cancellationToken).ConfigureAwait(false);

        if (takenByAccount || takenByPending)
        {
            return Result.Failure(AuthErrors.EmailChangeAddressInUse);
        }

        DateTimeOffset now = _clock.UtcNow;

        // Only the newest request should be confirmable.
        await _pendingChanges.InvalidateAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        GeneratedToken generated = _tokenGenerator.Generate();

        var pending = new PendingEmailChange(
            Guid.CreateVersion7(),
            user.Id,
            newEmail,
            generated.Hash,
            now.AddHours(_auth.EmailChangeTokenLifetimeHours),
            now);

        await _pendingChanges.AddAsync(pending, cancellationToken).ConfigureAwait(false);

        user.BeginEmailChange(newEmail, now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildEmailChangeConfirmationEmail(
                newEmail.Value,
                user.FullName,
                _urls.BuildConfirmEmailChangeUrl(generated.RawValue)),
            cancellationToken).ConfigureAwait(false);

        // Sent to the address currently on file, which is still the authoritative one.
        await _emailSender.SendAsync(
            _templates.BuildEmailChangeNoticeEmail(user.Email.Value, user.FullName, newEmail.Value),
            cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.EmailChangeRequested,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { newEmail = newEmail.Value },
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
