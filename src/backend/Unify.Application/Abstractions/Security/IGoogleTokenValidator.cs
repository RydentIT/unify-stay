namespace Unify.Application.Abstractions.Security;

/// <summary>
/// The claims we trust from a Google ID token, after server-side signature validation.
/// <paramref name="GivenName"/>/<paramref name="FamilyName"/> populate the account's first and
/// last name; <paramref name="Name"/> (the full display name) is kept only as a fallback for
/// tokens that omit the split claims.
/// </summary>
public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    bool EmailVerified,
    string? Name,
    string? GivenName,
    string? FamilyName);

/// <summary>
/// Verifies a Google ID token against Google's published keys.
///
/// The whole point of this abstraction is that the email and the verified flag come from a
/// signature-checked token, never from something the browser asserted - REG-004 hands over an
/// existing account on the strength of that flag, so trusting the client would be an
/// account-takeover hole.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>Returns null when the token is invalid, expired, or issued to another audience.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
