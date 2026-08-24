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
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;

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

        DateTimeOffset issuedAt = _dateTimeProvider.UtcNow;

        int lifetimeMinutes = scope == TokenScope.PasswordChangeOnly
            ? _options.PasswordChangeTokenLifetimeMinutes
            : _options.AccessTokenLifetimeMinutes;

        DateTimeOffset expiresAt = issuedAt.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(UnifyClaimTypes.TokenType, scope == TokenScope.PasswordChangeOnly
                ? TokenTypeValues.PasswordChange
                : TokenTypeValues.Full),
        };

        // A password-change token carries no roles at all: even if an endpoint forgot the
        // scope policy, there is nothing on the token for a role check to succeed against.
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
