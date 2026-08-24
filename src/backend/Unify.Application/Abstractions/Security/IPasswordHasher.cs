namespace Unify.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifies a candidate password. Implementations must be constant-time with respect to
    /// the hash contents and must return false (never throw) for a malformed stored hash.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
