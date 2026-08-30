using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Domain.Users;

namespace Unify.Infrastructure.Security;

/// <summary>
/// Issues HS256 access tokens using JsonWebTokenHandler (the current handler; the legacy
/// JwtSecurityTokenHandler is deliberately not used).
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly IDateTimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _clock = clock;

        byte[] keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);

        if (keyBytes.Length < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyBytes} bytes for HS256.");
        }

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(
        Guid userId,
        string email,
        IReadOnlyCollection<RoleName> roles,
        TokenScope scope = TokenScope.Full)
    {
        ArgumentNullException.ThrowIfNull(roles);

        DateTimeOffset issuedAt = _clock.UtcNow;

        // Both limited scopes are short-lived recovery tokens that exist only to carry the user
        // from login to the one endpoint that clears their gate, so they share a lifetime.
        int lifetimeMinutes = scope == TokenScope.Full
            ? _options.AccessTokenLifetimeMinutes
            : _options.PasswordChangeTokenLifetimeMinutes;

        DateTimeOffset expiresAt = issuedAt.AddMinutes(lifetimeMinutes);

        string tokenType = scope switch
        {
            TokenScope.PasswordChangeRequired => TokenTypeValues.PasswordChangeRequired,
            TokenScope.ProfileCompletionRequired => TokenTypeValues.ProfileCompletionRequired,
            _ => TokenTypeValues.Full,
        };

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(UnifyClaimTypes.TokenType, tokenType),
        };

        // A limited-scope token carries no roles at all: even if an endpoint forgot its policy,
        // there is nothing on the token for a role check to succeed against.
        if (scope == TokenScope.Full)
        {
            claims.AddRange(roles.Select(role => new Claim(UnifyClaimTypes.Role, role.ToString())));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt, scope);
    }
}
