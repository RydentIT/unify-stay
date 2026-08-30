namespace Unify.Application.Common;

/// <summary>
/// The failures the user-management modules can return.
///
/// Several of these are deliberately vague, and that vagueness is the requirement rather than
/// laziness: <see cref="InvalidCredentials"/> is one message for every login failure mode
/// (BR-LOG-001), and the forgot-password flow returns success even for unknown addresses
/// (BR-FPW-002). Anything sharper would turn these endpoints into account enumeration oracles.
/// </summary>
public static class AuthErrors
{
    // ---------- Registration ----------
    public static readonly Error EmailAlreadyRegistered = Error.Conflict(
        "auth.register.email_in_use",
        "An account with this email address already exists.");

    /// <summary>REG-005: Google address not verified, so we cannot safely link to the existing account.</summary>
    public static readonly Error UnverifiedGoogleEmailCollision = Error.Conflict(
        "auth.register.unverified_google_collision",
        "An account already exists for this email address. Sign in with your password, or verify your email with Google and try again.");

    public static readonly Error TermsNotAccepted = Error.Validation(
        "auth.register.terms_not_accepted",
        "The terms of service must be accepted.");

    // ---------- Login ----------
    /// <summary>BR-LOG-001: identical for wrong password, unknown address, and wrong provider.</summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "auth.login.invalid_credentials",
        "Invalid email or password.");

    public static readonly Error EmailNotVerified = Error.Forbidden(
        "auth.login.email_not_verified",
        "Please verify your email address before signing in.");

    public static readonly Error AccountDeleted = Error.Forbidden(
        "auth.login.account_deleted",
        "This account is no longer available.");

    /// <summary>
    /// Deliberate exception to BR-LOG-001's enumeration-safety: a banned user needs to know why,
    /// not assume a typo. Never reveals the suspension reason itself, only that it is suspended.
    /// </summary>
    public static readonly Error AccountSuspended = Error.Forbidden(
        "auth.login.account_suspended",
        "This account has been suspended. Contact support for more information.");

    public static Error AccountLocked(DateTimeOffset until) => new(
        "auth.login.locked",
        $"Too many failed attempts. Try again after {until:HH:mm} UTC.",
        ErrorType.RateLimited);

    /// <summary>LOG-016: staff accounts must not come in through the Google button.</summary>
    public static readonly Error GoogleNotAllowedForRole = Error.Forbidden(
        "auth.login.google_not_allowed",
        "This account must sign in with an email address and password.");

    public static readonly Error InvalidGoogleToken = Error.Unauthorized(
        "auth.login.invalid_google_token",
        "Google sign-in could not be verified.");

    public static readonly Error GoogleNotConfigured = Error.Unexpected(
        "auth.google.not_configured",
        "Google sign-in is not configured on this server.");

    // ---------- Password ----------
    public static readonly Error InvalidResetToken = Error.Validation(
        "auth.password.invalid_reset_token",
        "This password reset link is not valid.");

    public static readonly Error ExpiredResetToken = Error.Validation(
        "auth.password.expired_reset_token",
        "This password reset link has expired. Please request a new one.");

    public static readonly Error UsedResetToken = Error.Validation(
        "auth.password.used_reset_token",
        "This password reset link has already been used.");

    /// <summary>FPW-007 / PRF-005.</summary>
    public static readonly Error PasswordMatchesCurrent = Error.Validation(
        "auth.password.matches_current",
        "The new password must be different from your current password.");

    public static readonly Error CurrentPasswordIncorrect = Error.Validation(
        "auth.password.current_incorrect",
        "Your current password is incorrect.");

    /// <summary>PRF-012 / BR-PRF-004: no password exists to change.</summary>
    public static readonly Error NoPasswordForGoogleAccount = Error.Conflict(
        "auth.password.google_only_account",
        "This account signs in with Google and has no password to change.");

    // ---------- Email verification ----------
    public static readonly Error InvalidVerificationToken = Error.Validation(
        "auth.email.invalid_token",
        "This verification link is not valid.");

    public static readonly Error ExpiredVerificationToken = Error.Validation(
        "auth.email.expired_token",
        "This verification link has expired. Please request a new one.");

    public static readonly Error UsedVerificationToken = Error.Validation(
        "auth.email.used_token",
        "This email address has already been verified.");

    public static readonly Error AlreadyVerified = Error.Conflict(
        "auth.email.already_verified",
        "This email address is already verified.");

    // ---------- Profile ----------
    public static readonly Error UserNotFound = Error.NotFound(
        "user.not_found",
        "Account not found.");

    /// <summary>PRF-011 / AC-PRF-009: every profile route is closed while a reset is outstanding.</summary>
    public static readonly Error PasswordChangeRequired = Error.Forbidden(
        "auth.password_change_required",
        "You must change your password before using this feature.");

    /// <summary>
    /// Mirrors <see cref="PasswordChangeRequired"/> for Google accounts missing a phone number:
    /// every profile/settings route is closed until they complete their profile.
    /// </summary>
    public static readonly Error ProfileCompletionRequired = Error.Forbidden(
        "auth.profile_completion_required",
        "You must add a phone number before using this feature.");

    public static readonly Error EmailChangeAddressInUse = Error.Conflict(
        "profile.email.address_in_use",
        "That email address is already in use.");

    public static readonly Error NoPendingEmailChange = Error.NotFound(
        "profile.email.no_pending_change",
        "There is no pending email change for this account.");

    // ---------- Settings ----------
    public static readonly Error UpgradeRequestAlreadyActive = Error.Conflict(
        "settings.upgrade.already_active",
        "You already have an upgrade request in progress.");

    /// <summary>SET-008: resubmission is only open after a rejection.</summary>
    public static readonly Error UpgradeRequestNotRejected = Error.Conflict(
        "settings.upgrade.not_rejected",
        "You can only resubmit after a request has been rejected.");

    public static readonly Error UpgradeRequestNotFound = Error.NotFound(
        "settings.upgrade.not_found",
        "Upgrade request not found.");

    public static readonly Error UpgradeRequestAlreadyDecided = Error.Conflict(
        "settings.upgrade.already_decided",
        "This upgrade request has already been decided.");

    /// <summary>Phone 2 must differ from the account's existing (phone 1) contact number.</summary>
    public static readonly Error SecondPhoneMatchesPrimary = Error.Validation(
        "settings.upgrade.second_phone_matches_primary",
        "The second phone number must be different from your registered phone number.");

    public static readonly Error AccountAlreadyDeactivated = Error.Conflict(
        "settings.account.already_deactivated",
        "This account is already deactivated.");

    // ---------- Admin: user directory & suspension ----------
    public static readonly Error UserAlreadySuspended = Error.Conflict(
        "admin.suspend.already_suspended",
        "This account is already suspended.");

    /// <summary>A deactivated or deleted account is not a valid suspension target.</summary>
    public static readonly Error UserNotEligibleForSuspension = Error.Conflict(
        "admin.suspend.not_eligible",
        "This account cannot be suspended (it is deactivated or deleted).");

    public static readonly Error NoActiveSuspension = Error.Conflict(
        "admin.suspend.no_active_suspension",
        "There is no active suspension to lift on this account.");

    public static readonly Error SuspensionReasonRequired = Error.Validation(
        "admin.suspend.reason_required",
        "A reason is required to suspend an account.");

    public static readonly Error InvalidRoleFilter = Error.Validation(
        "admin.users.invalid_role",
        "Unrecognised role filter.");

    public static readonly Error InvalidStatusFilter = Error.Validation(
        "admin.users.invalid_status",
        "Unrecognised status filter.");
}
