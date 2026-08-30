using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// Google sign-in.
///
/// LOG-016 is the rule that shapes this: Google is available to Students and Property Owners
/// only. Admin and Staff must come through email + password, so that privileged access does not
/// depend on an external identity provider's account recovery.
/// </summary>
internal sealed class LoginWithGoogleCommandHandler : ICommandHandler<LoginWithGoogleCommand, Result<LoginResult>>
{
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IUserRepository _users;
    private readonly LoginSessionIssuer _sessionIssuer;
    private readonly LoginLockoutPolicy _lockout;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public LoginWithGoogleCommandHandler(
        IGoogleTokenValidator googleTokenValidator,
        IUserRepository users,
        LoginSessionIssuer sessionIssuer,
        LoginLockoutPolicy lockout,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _googleTokenValidator = googleTokenValidator;
        _users = users;
        _sessionIssuer = sessionIssuer;
        _lockout = lockout;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<LoginResult>> HandleAsync(
        LoginWithGoogleCommand request,
        CancellationToken cancellationToken)
    {
        GoogleUserInfo? googleUser = await _googleTokenValidator
            .ValidateAsync(request.IdToken, cancellationToken)
            .ConfigureAwait(false);

        if (googleUser is null)
        {
            await LogAsync(AuditActions.LoginFailed, null, null, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.InvalidGoogleToken);
        }

        var email = Email.Create(googleUser.Email);

        // Prefer the provider link; fall back to the address so an account whose Google identity
        // was linked under a different subject still resolves.
        User? user = await _users
            .GetByProviderAsync(AuthProviderKind.Google, googleUser.Subject, cancellationToken)
            .ConfigureAwait(false)
            ?? await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            await LogAsync(AuditActions.LoginFailed, null, email.Value, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // The account exists under this address but has never been linked to Google, and Google
        // has not proven ownership - the same condition REG-005 refuses to link on.
        if (!user.HasProvider(AuthProviderKind.Google) && !googleUser.EmailVerified)
        {
            await LogAsync(AuditActions.LoginFailed, user.Id, email.Value, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // LOG-009: deleted accounts are refused regardless of provider.
        if (user.IsDeleted)
        {
            await LogAsync(AuditActions.LoginBlockedInactive, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // Suspension (ban) extends LOG-009. Google has already verified this caller is the real
        // owner of the identity (the token was validated above), so a distinct message here is
        // not an anonymous-enumeration oracle the way it would be for an unproven caller.
        if (user.Status == UserStatus.Suspended)
        {
            await LogAsync(AuditActions.LoginBlockedSuspended, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<LoginResult>(AuthErrors.AccountSuspended);
        }

        // LOG-016: privileged roles may not enter through Google.
        if (user.Roles.Any(role => role is RoleName.Admin or RoleName.Staff))
        {
            await LogAsync(AuditActions.LoginFailed, user.Id, email.Value, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LoginResult>(AuthErrors.GoogleNotAllowedForRole);
        }

        DateTimeOffset now = _clock.UtcNow;

        if (user.Status == UserStatus.Deactivated)
        {
            user.Reactivate(now);
            await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

            await LogAsync(AuditActions.AccountReactivated, user.Id, email.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        await _lockout
            .RecordSuccessAsync(user.Id, email.Value, _currentUser.IpAddress, cancellationToken)
            .ConfigureAwait(false);

        LoginResult result = await _sessionIssuer
            .IssueAsync(user, request.RememberMe, cancellationToken)
            .ConfigureAwait(false);

        await LogAsync(
            result.MustChangePassword ? AuditActions.LoginPasswordChangeRequired : AuditActions.LoginSucceeded,
            user.Id,
            email.Value,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(result);
    }

    private Task LogAsync(string action, Guid? userId, string? email, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = action,
                UserId = userId,
                Email = email,
                Provider = nameof(AuthProviderKind.Google),
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken);
}
