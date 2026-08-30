using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Register;

/// <summary>
/// Google registration, including the collision rules.
///
/// The decision that matters here is what to do when the Google address already belongs to an
/// account created some other way:
///   * Google says the address is verified  -> link the provider onto that account (REG-004).
///   * Google does NOT say it is verified   -> refuse, with a message that explains the options
///                                              (REG-005). Linking on an unverified address
///                                              would let anyone who can mint such a token take
///                                              over an existing account.
/// Either way the account skips our own email verification, because Google already proved
/// ownership (BR-REG-004).
/// </summary>
internal sealed class RegisterWithGoogleCommandHandler
    : ICommandHandler<RegisterWithGoogleCommand, Result<GoogleRegisterResult>>
{
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IUserRepository _users;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public RegisterWithGoogleCommandHandler(
        IGoogleTokenValidator googleTokenValidator,
        IUserRepository users,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _googleTokenValidator = googleTokenValidator;
        _users = users;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<GoogleRegisterResult>> HandleAsync(
        RegisterWithGoogleCommand request,
        CancellationToken cancellationToken)
    {
        GoogleUserInfo? googleUser = await _googleTokenValidator
            .ValidateAsync(request.IdToken, cancellationToken)
            .ConfigureAwait(false);

        if (googleUser is null)
        {
            await LogFailureAsync(AuditActions.RegisterFailed, email: null, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<GoogleRegisterResult>(AuthErrors.InvalidGoogleToken);
        }

        var email = Email.Create(googleUser.Email);
        DateTimeOffset now = _clock.UtcNow;

        // Already linked: this is a returning user hitting the register button. Treat it as a
        // no-op success rather than an error, since there is nothing to fix.
        User? byProvider = await _users
            .GetByProviderAsync(AuthProviderKind.Google, googleUser.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (byProvider is not null)
        {
            return Result.Success(
                new GoogleRegisterResult(byProvider.Id, byProvider.Email.Value, LinkedToExistingAccount: true));
        }

        User? existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return await HandleCollisionAsync(existing, googleUser, email, now, cancellationToken)
                .ConfigureAwait(false);
        }

        (string firstName, string lastName) = ResolveName(googleUser, email);

        var user = User.RegisterWithGoogle(
            Guid.CreateVersion7(),
            firstName,
            lastName,
            email,
            now);

        user.AddAuthProvider(AuthProvider.Google(
            Guid.CreateVersion7(),
            user.Id,
            googleUser.Subject,
            googleUser.EmailVerified,
            now));

        await _users.AddAsync(user, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.RegisterSucceeded,
                UserId = user.Id,
                Email = email.Value,
                Provider = nameof(AuthProviderKind.Google),
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { role = nameof(RoleName.Student), emailVerificationSkipped = true },
            },
            cancellationToken).ConfigureAwait(false);

        // No verification email: Google already vouched for the address (BR-REG-004).
        await _emailSender.SendAsync(
            _templates.BuildGoogleWelcomeEmail(email.Value, user.FullName),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new GoogleRegisterResult(user.Id, email.Value, LinkedToExistingAccount: false));
    }

    private async Task<Result<GoogleRegisterResult>> HandleCollisionAsync(
        User existing,
        GoogleUserInfo googleUser,
        Email email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // REG-005: without Google's own confirmation that the user owns this address, linking
        // would hand over someone else's account.
        if (!googleUser.EmailVerified)
        {
            await _auditLogger.LogAsync(
                new AuditEvent
                {
                    ActionType = AuditActions.RegisterFailed,
                    UserId = existing.Id,
                    Email = email.Value,
                    Provider = nameof(AuthProviderKind.Google),
                    IpAddress = _currentUser.IpAddress,
                    FieldsChanged = new { reason = "google_email_unverified" },
                },
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<GoogleRegisterResult>(AuthErrors.UnverifiedGoogleEmailCollision);
        }

        // REG-004: verified, so attach Google as an additional way into the SAME account.
        var provider = AuthProvider.Google(
            Guid.CreateVersion7(),
            existing.Id,
            googleUser.Subject,
            emailVerified: true,
            now);

        await _users.AddAuthProviderAsync(provider, cancellationToken).ConfigureAwait(false);

        // Google's confirmation also settles any outstanding verification on the account.
        if (!existing.EmailVerified)
        {
            existing.MarkEmailVerified(now);
            await _users.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.ProviderLinked,
                UserId = existing.Id,
                Email = email.Value,
                Provider = nameof(AuthProviderKind.Google),
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken).ConfigureAwait(false);

        // The account owner did not necessarily do this, so tell them it happened.
        await _emailSender.SendAsync(
            _templates.BuildAccountLinkedEmail(email.Value, existing.FullName, nameof(AuthProviderKind.Google)),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(
            new GoogleRegisterResult(existing.Id, email.Value, LinkedToExistingAccount: true));
    }

    private Task LogFailureAsync(string action, string? email, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = action,
                Email = email,
                Provider = nameof(AuthProviderKind.Google),
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken);

    /// <summary>
    /// Prefers Google's own given_name/family_name claims. Falls back to splitting the full
    /// display name on the first space, and finally to the email's local part, so a token that
    /// omits every name claim still produces a usable account rather than failing registration.
    /// </summary>
    private static (string FirstName, string LastName) ResolveName(GoogleUserInfo googleUser, Email email)
    {
        if (!string.IsNullOrWhiteSpace(googleUser.GivenName))
        {
            string last = string.IsNullOrWhiteSpace(googleUser.FamilyName) ? "-" : googleUser.FamilyName;
            return (googleUser.GivenName, last);
        }

        if (!string.IsNullOrWhiteSpace(googleUser.Name))
        {
            string[] parts = googleUser.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "-");
        }

        return (email.Value.Split('@')[0], "-");
    }
}
