namespace Unify.Domain.Users;

/// <summary>
/// A row from the roles catalogue. Identified by a small fixed integer rather than a GUID, so
/// it does not derive from Entity.
/// </summary>
public sealed record Role(RoleName Name)
{
    public int Id => (int)Name;

    public static Role Of(RoleName name) => new(name);
}
