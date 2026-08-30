namespace Unify.Infrastructure.Security;

/// <summary>
/// Claim names shared between the token issuer here and the bearer configuration in the API.
/// Constants, so a rename cannot leave the two halves silently disagreeing.
/// </summary>
public static class UnifyClaimTypes
{
    public const string Role = "role";

    /// <summary>Carries the token scope. See <see cref="TokenTypeValues"/>.</summary>
    public const string TokenType = "token_type";

    /// <summary>Session this token was issued against, so change-password can spare it.</summary>
    public const string SessionId = "sid";
}

public static class TokenTypeValues
{
    public const string Full = "full";

    /// <summary>
    /// Issued to a user with must_change_password set. Authenticates, but authorises nothing
    /// except the change-password endpoint (LOG-013, BR-LOG-008).
    /// </summary>
    public const string PasswordChangeRequired = "password_change_required";

    /// <summary>
    /// Issued to a Google-registered user with must_complete_profile set. Authenticates, but
    /// authorises nothing except the complete-profile endpoint.
    /// </summary>
    public const string ProfileCompletionRequired = "profile_completion_required";
}
