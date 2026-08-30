using Unify.Application.Abstractions.Security;

namespace Unify.Infrastructure.Security;

/// <summary>
/// BCrypt with a per-hash salt. The work factor is a deliberate cost: raise it as hardware
/// improves. Existing hashes keep their original factor until rehashed on a later sign-in.
/// </summary>
internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string? passwordHash)
    {
        // A null hash means a Google-only account. Returning false (rather than throwing, or
        // worse, succeeding) is what stops "no password" from meaning "any password".
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
            // would let a caller tell a corrupt record apart from a wrong password.
            return false;
        }
    }
}
