using Unify.Application.Abstractions.Security;
using Unify.Domain.Users;

namespace Unify.Application.Abstractions.Identity;

/// <summary>
/// The caller behind the current request. Implemented in the API layer over HttpContext;
/// handlers depend on this instead of reaching for HttpContext themselves.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<RoleName> Roles { get; }

    bool IsAuthenticated { get; }

    /// <summary>Null when unauthenticated. See <see cref="TokenScope"/> for what this gates.</summary>
    TokenScope? Scope { get; }
}
