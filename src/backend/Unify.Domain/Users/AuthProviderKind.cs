namespace Unify.Domain.Users;

public enum AuthProviderKind
{
    /// <summary>Email + password held in our own database.</summary>
    Local = 0,

    Google = 1,
}
