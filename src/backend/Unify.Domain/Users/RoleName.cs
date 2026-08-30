namespace Unify.Domain.Users;

/// <summary>
/// The seeded roles. Values match the fixed ids in the roles table (migration 0002) so the
/// enum and the catalogue cannot drift apart.
/// </summary>
public enum RoleName
{
    /// <summary>Default role granted at registration (REG-007).</summary>
    Student = 1,

    /// <summary>Granted only by an approved upgrade request (SET-009).</summary>
    PropertyOwner = 2,

    Admin = 3,

    Staff = 4,
}
