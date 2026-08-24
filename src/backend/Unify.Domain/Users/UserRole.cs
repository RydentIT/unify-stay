using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>A role grant. Rows live in the user_roles table.</summary>
public sealed class UserRole : Entity
{
    public UserRole(Guid id, Guid userId, RoleName role, DateTimeOffset grantedAtUtc)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserRole must belong to a user.");
        }

        UserId = userId;
        Role = role;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid UserId { get; }

    public RoleName Role { get; }

    public DateTimeOffset GrantedAtUtc { get; }
}
