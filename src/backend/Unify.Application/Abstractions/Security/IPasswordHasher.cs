namespace Unify.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifies a candidate password. Must return false rather than throwing for a malformed or
    /// missing stored hash, so a corrupt record is indistinguishable from a wrong password.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}
