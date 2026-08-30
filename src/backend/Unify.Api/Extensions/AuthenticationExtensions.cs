using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Unify.Domain.Users;
using Unify.Infrastructure.Security;

namespace Unify.Api.Extensions;

/// <summary>Authorization policy names used by the endpoints.</summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// The default for anything real: an authenticated caller holding a full-scope token. A
    /// forced-reset token is explicitly rejected here.
    /// </summary>
    public const string FullAccess = "full-access";

    /// <summary>
    /// The only policy a password-change-only token satisfies. Applied to the change-password
    /// endpoint so a user with must_change_password can complete the flow and nothing else.
    /// </summary>
    public const string PasswordChangeOnly = "password-change-only";

    /// <summary>
    /// Mirrors <see cref="PasswordChangeOnly"/> for a Google account missing a phone number.
    /// The only policy a profile-completion-only token satisfies.
    /// </summary>
    public const string ProfileCompletionOnly = "profile-completion-only";

    public const string AdminOnly = "admin-only";

    /// <summary>
    /// Any authenticated token, regardless of scope. Used only where being signed in at all is
    /// the entire requirement - logout is the one case, since a user mid-recovery on either
    /// limited scope must still be able to end their session.
    /// </summary>
    public const string AnyScope = "any-scope";
}

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddUnifyAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured through IConfigureOptions rather than inline, so the signing key is read
        // from the fully-built configuration at resolve time. Reading it during registration
        // captured whatever was bound at that moment, which silently diverged from the key
        // JwtTokenService resolves later - producing tokens the API then rejected as unsigned.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.FullAccess, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(UnifyClaimTypes.TokenType, TokenTypeValues.Full))
            .AddPolicy(AuthorizationPolicies.PasswordChangeOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(
                    UnifyClaimTypes.TokenType,
                    TokenTypeValues.PasswordChangeRequired,
                    TokenTypeValues.Full))
            .AddPolicy(AuthorizationPolicies.ProfileCompletionOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(
                    UnifyClaimTypes.TokenType,
                    TokenTypeValues.ProfileCompletionRequired,
                    TokenTypeValues.Full))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(UnifyClaimTypes.TokenType, TokenTypeValues.Full)
                .RequireRole(nameof(RoleName.Admin)))
            .AddPolicy(AuthorizationPolicies.AnyScope, policy => policy
                .RequireAuthenticatedUser())
            // Endpoints must opt in explicitly. An [Authorize] with no policy named gets
            // FullAccess, so forgetting the policy name cannot admit a forced-reset token.
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(UnifyClaimTypes.TokenType, TokenTypeValues.Full)
                .Build());

        return services;
    }
}

/// <summary>
/// Binds the bearer scheme from JwtOptions. Separate class so the options are resolved from DI
/// after configuration is complete.
/// </summary>
internal sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt;

    public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        _jwt = jwt.Value;
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name is not null && name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(options);

        // The modern handler. The legacy JwtSecurityTokenHandler is not used, and its inbound
        // claim-type mapping is off so claims arrive exactly as issued.
        options.TokenHandlers.Clear();
        options.TokenHandlers.Add(new JsonWebTokenHandler());
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(_jwt.ClockSkewSeconds),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = UnifyClaimTypes.Role,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };
    }
}
