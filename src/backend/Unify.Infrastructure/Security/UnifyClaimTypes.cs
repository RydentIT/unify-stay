namespace Unify.Infrastructure.Security;

/// <summary>
/// Claim names shared between the token issuer here and the bearer configuration in the API.
/// Kept as constants so a rename cannot leave the two halves disagreeing.
/// </summary>
public static class UnifyClaimTypes
{
    public const string Role = "role";

    /// <summary>Carries the token scope. See <see cref="TokenTypeValues"/>.</summary>
    public const string TokenType = "token_type";
}

public static class TokenTypeValues
{
    public const string Full = "full";

    /// <summary>
    /// A token issued to a user with must_change_password set. It authenticates the caller but
    /// authorises nothing except the change-password endpoint.
    /// </summary>
    public const string PasswordChange = "password_change";
}
