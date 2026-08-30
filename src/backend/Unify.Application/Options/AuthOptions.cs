namespace Unify.Application.Options;

/// <summary>
/// Lockout and token-lifetime knobs. BR-LOG-002 requires the threshold and lockout duration to
/// be configurable rather than baked into the code, so every value here is bound from
/// configuration and can be tuned per environment without a redeploy.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Consecutive failures before an account is locked (LOG-006).</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>How long the lockout lasts once the threshold is hit.</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Window over which consecutive failures are counted.</summary>
    public int FailedAttemptWindowMinutes { get; set; } = 15;

    /// <summary>Failures from one IP across any account before that IP is throttled (LOG-005).</summary>
    public int MaxFailedAttemptsPerIpAddress { get; set; } = 20;

    public int PasswordResetTokenLifetimeMinutes { get; set; } = 60;

    public int EmailVerificationTokenLifetimeHours { get; set; } = 24;

    public int EmailChangeTokenLifetimeHours { get; set; } = 24;

    /// <summary>Refresh lifetime for an ordinary session.</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    /// <summary>
    /// Refresh lifetime when "Remember Me" was ticked. Only the REFRESH token is extended - the
    /// access token lifetime never changes (BR-LOG-005).
    /// </summary>
    public int RememberMeRefreshTokenLifetimeDays { get; set; } = 30;

    public int MinimumPasswordLength { get; set; } = 12;

    public TimeSpan FailedAttemptWindow => TimeSpan.FromMinutes(FailedAttemptWindowMinutes);

    public TimeSpan Lockout => TimeSpan.FromMinutes(LockoutMinutes);
}
