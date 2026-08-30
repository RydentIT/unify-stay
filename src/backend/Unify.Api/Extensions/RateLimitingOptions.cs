namespace Unify.Api.Extensions;

public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowMinutes { get; set; } = 1;
}

/// <summary>
/// Bound from the "RateLimiting" section. Limits live in configuration so they can be tuned
/// per environment without a redeploy of new code.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitWindow Registration { get; set; } = new() { PermitLimit = 5, WindowMinutes = 15 };

    public RateLimitWindow Login { get; set; } = new() { PermitLimit = 10, WindowMinutes = 15 };

    public RateLimitWindow PasswordReset { get; set; } = new() { PermitLimit = 3, WindowMinutes = 1 };

    /// <summary>EVR-006. Complements the per-account throttle inside the handler.</summary>
    public RateLimitWindow ResendVerification { get; set; } = new() { PermitLimit = 3, WindowMinutes = 60 };

    public RateLimitWindow Global { get; set; } = new() { PermitLimit = 300, WindowMinutes = 1 };
}

/// <summary>Policy names shared between registration and the endpoints that opt into them.</summary>
public static class RateLimitPolicies
{
    public const string Registration = "registration";
    public const string Login = "login";
    public const string PasswordReset = "password-reset";
    public const string ResendVerification = "resend-verification";
}
