using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Security;
using Unify.Domain.Users;
using Unify.Infrastructure.Security;

namespace Unify.Api.Identity;

/// <summary>
/// Reads the caller off the current HttpContext so handlers can depend on ICurrentUser rather
/// than on ASP.NET Core types.
/// </summary>
internal sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;

    public string? Email => Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public IReadOnlyCollection<RoleName> Roles =>
        Principal is null
            ? []
            : [.. Principal
                .FindAll(UnifyClaimTypes.Role)
                .Select(claim => Enum.TryParse(claim.Value, out RoleName role)
                    ? (RoleName?)role
                    : null)
                .Where(role => role is not null)
                .Select(role => role!.Value)];

    public TokenScope? Scope
    {
        get
        {
            if (!IsAuthenticated)
            {
                return null;
            }

            return Principal?.FindFirst(UnifyClaimTypes.TokenType)?.Value switch
            {
                TokenTypeValues.PasswordChange => TokenScope.PasswordChangeOnly,
                TokenTypeValues.Full => TokenScope.Full,
                _ => null,
            };
        }
    }
}
