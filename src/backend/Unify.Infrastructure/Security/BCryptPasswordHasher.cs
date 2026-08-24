using Unify.Application.Abstractions.Security;

namespace Unify.Infrastructure.Security;

/// <summary>
/// BCrypt with a per-hash salt. The work factor is a deliberate cost: raise it as hardware
/// improves, and note that existing hashes keep their original factor until rehashed on the
/// next successful sign-in.
/// </summary>
internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A stored hash we cannot parse is a failed verification, never an exception that
            // leaks the difference between a bad password and a corrupt record.
            return false;
        }
    }
}
