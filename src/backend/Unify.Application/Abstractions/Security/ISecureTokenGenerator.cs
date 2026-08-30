namespace Unify.Application.Abstractions.Security;

/// <summary>A freshly minted token: the raw value goes in the email, only the hash is stored.</summary>
public sealed record GeneratedToken(string RawValue, string Hash);

/// <summary>
/// Produces cryptographically random tokens for verification/reset links and refresh tokens.
/// Hashing is exposed separately so lookups can hash an incoming value and compare.
/// </summary>
public interface ISecureTokenGenerator
{
    GeneratedToken Generate();

    /// <summary>Hashes a raw token so it can be matched against stored hashes.</summary>
    string Hash(string rawValue);
}
