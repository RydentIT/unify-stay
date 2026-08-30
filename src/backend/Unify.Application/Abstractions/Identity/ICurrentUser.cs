using Unify.Application.Abstractions.Security;
using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Identity;

/// <summary>
/// The caller behind the current request, and the request's origin. Implemented in the API over
/// HttpContext so handlers never reach for HttpContext themselves.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<RoleName> Roles { get; }

    bool IsAuthenticated { get; }

    /// <summary>Null when unauthenticated. Anything other than Full is a restricted credential.</summary>
    TokenScope? Scope { get; }

    /// <summary>The session this token was issued against, so change-password can spare it (BR-PRF-003).</summary>
    Guid? SessionId { get; }

    /// <summary>Caller IP, recorded on audit entries and login attempts.</summary>
    string? IpAddress { get; }
}
