namespace Unify.Application.Abstractions.Auditing;

/// <summary>
/// The action names written to audit_logs. Constants rather than free strings so that a typo
/// cannot quietly create a parallel event name that no report will ever find.
/// </summary>
public static class AuditActions
{
    // Register
    public const string RegisterSucceeded = "user.register.succeeded";
    public const string RegisterFailed = "user.register.failed";
    public const string RegisterEmailInUse = "user.register.email_in_use";
    public const string ProviderLinked = "user.provider.linked";

    // Login
    public const string LoginSucceeded = "user.login.succeeded";
    public const string LoginFailed = "user.login.failed";
    public const string LoginBlockedUnverified = "user.login.blocked_unverified";
    public const string LoginBlockedInactive = "user.login.blocked_inactive";
    public const string LoginLocked = "user.login.locked";
    public const string LoginBlockedSuspended = "user.login.blocked_suspended";
    public const string LoginPasswordChangeRequired = "user.login.password_change_required";
    public const string Logout = "user.logout";
    public const string AccountReactivated = "user.account.reactivated";

    // Password
    public const string PasswordResetRequested = "user.password.reset_requested";
    public const string PasswordResetCompleted = "user.password.reset_completed";
    public const string PasswordResetTokenRejected = "user.password.reset_token_rejected";
    public const string PasswordChanged = "user.password.changed";
    public const string ForcedPasswordChanged = "user.password.forced_change_completed";

    // Email verification
    public const string VerificationEmailSent = "user.email.verification_sent";
    public const string EmailVerified = "user.email.verified";
    public const string EmailVerificationFailed = "user.email.verification_failed";

    // Profile
    public const string ProfileUpdated = "profile.updated";
    public const string ProfileCompleted = "profile.completed";
    public const string EmailChangeRequested = "profile.email.change_requested";
    public const string EmailChangeCompleted = "profile.email.change_completed";
    public const string AvatarUpdated = "profile.avatar.updated";

    // Settings
    public const string UpgradeRequested = "settings.upgrade.requested";
    public const string UpgradeResubmitted = "settings.upgrade.resubmitted";
    public const string UpgradeApproved = "settings.upgrade.approved";
    public const string UpgradeRejected = "settings.upgrade.rejected";
    public const string AccountDeactivated = "settings.account.deactivated";
    public const string AccountDeleted = "settings.account.deleted";

    // Admin: user directory & suspension
    public const string UserSuspended = "admin.user.suspended";
    public const string SuspensionLifted = "admin.user.suspension_lifted";
}
