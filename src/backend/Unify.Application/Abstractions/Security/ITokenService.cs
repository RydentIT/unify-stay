using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Security;

/// <summary>
/// What an issued token is allowed to do. A <see cref="PasswordChangeOnly"/> token is the
/// forced-reset credential: it authenticates the user but unlocks nothing except the
/// change-password endpoint.
/// </summary>
public enum TokenScope
{
    Full = 0,
    PasswordChangeOnly = 1,
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc, TokenScope Scope);

public interface ITokenService
{
    AccessToken CreateAccessToken(
        Guid userId,
        string email,
        IReadOnlyCollection<RoleName> roles,
        TokenScope scope = TokenScope.Full);
}
