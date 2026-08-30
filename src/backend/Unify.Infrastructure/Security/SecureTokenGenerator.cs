using System.Security.Cryptography;
using System.Text;
using Unify.Application.Abstractions.Security;

namespace Unify.Infrastructure.Security;

/// <summary>
/// Generates the opaque tokens used in verification/reset links and as refresh tokens.
///
/// 32 bytes from a CSPRNG, base64url encoded so it survives being pasted into a URL. Storage
/// keeps only the SHA-256 hash: these values are bearer credentials, so a database leak must
/// not hand out working links. SHA-256 without a salt is deliberate and correct here - the
/// input already has 256 bits of entropy, so there is nothing for a rainbow table to precompute
/// and the lookup has to be by exact hash.
/// </summary>
internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenBytes = 32;

    public GeneratedToken Generate()
    {
        byte[] raw = RandomNumberGenerator.GetBytes(TokenBytes);
        string rawValue = Base64UrlEncode(raw);

        return new GeneratedToken(rawValue, Hash(rawValue));
    }

    public string Hash(string rawValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawValue);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
