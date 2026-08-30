using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Security;

/// <summary>
/// What an issued access token is allowed to do.
/// </summary>
public enum TokenScope
{
    Full = 0,

    /// <summary>
    /// The forced-reset credential (LOG-013/LOG-015/BR-LOG-008). It authenticates the user but
    /// authorises nothing except the change-password endpoint, and carries no role claims.
    /// </summary>
    PasswordChangeRequired = 1,

    /// <summary>
    /// Issued to a Google-registered account that has not yet supplied a phone number.
    /// Authenticates the user but authorises nothing except the complete-profile endpoint, and
    /// carries no role claims - the same shape as <see cref="PasswordChangeRequired"/>.
    /// </summary>
    ProfileCompletionRequired = 2,
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt, TokenScope Scope);

public interface ITokenService
{
    AccessToken CreateAccessToken(
        Guid userId,
        string email,
        IReadOnlyCollection<RoleName> roles,
        TokenScope scope = TokenScope.Full);
}
