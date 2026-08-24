namespace Unify.Domain.Users;

/// <summary>
/// Roles carried as claims on the access token. Persisted by name (not by ordinal) in the
/// user_roles table so that reordering this enum cannot silently re-grant permissions.
/// </summary>
public enum RoleName
{
    Guest = 0,
    Host = 1,
    Support = 2,
    Admin = 3,
}
