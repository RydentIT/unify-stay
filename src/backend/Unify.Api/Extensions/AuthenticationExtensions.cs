using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Unify.Infrastructure.Security;

namespace Unify.Api.Extensions;

/// <summary>Authorization policy names used by the endpoints.</summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// The default for anything real: requires an authenticated caller holding a full-scope
    /// token. A forced-reset token is explicitly rejected here.
    /// </summary>
    public const string FullAccess = "full-access";

    /// <summary>
    /// The only policy a password-change-only token satisfies. Applied to the change-password
    /// endpoint so that a user with must_change_password can complete the flow and nothing else.
    /// </summary>
    public const string PasswordChangeOnly = "password-change-only";

    public const string AdminOnly = "admin-only";
}

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddUnifyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // The modern handler. The legacy JwtSecurityTokenHandler is not used, and its
                // inbound claim-type mapping is off so claims arrive exactly as issued.
                options.TokenHandlers.Clear();
                options.TokenHandlers.Add(new JsonWebTokenHandler());
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            string.IsNullOrEmpty(jwt.SigningKey)
                                ? new string('0', JwtOptions.MinimumSigningKeyBytes)
                                : jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = UnifyClaimTypes.Role,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.FullAccess, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(UnifyClaimTypes.TokenType, TokenTypeValues.Full))
            .AddPolicy(AuthorizationPolicies.PasswordChangeOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(
                    UnifyClaimTypes.TokenType,
                    TokenTypeValues.PasswordChange,
                    TokenTypeValues.Full))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(UnifyClaimTypes.TokenType, TokenTypeValues.Full)
                .RequireRole(nameof(Unify.Domain.Users.RoleName.Admin)))
            // Endpoints must opt in explicitly. A [Authorize] with no policy gets FullAccess,
            // so forgetting the policy name cannot accidentally admit a forced-reset token.
            .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(UnifyClaimTypes.TokenType, TokenTypeValues.Full)
                .Build());

        return services;
    }
}
