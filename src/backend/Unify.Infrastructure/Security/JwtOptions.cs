using System.ComponentModel.DataAnnotations;

namespace Unify.Infrastructure.Security;

/// <summary>
/// Bound from the "Jwt" configuration section. Every value here comes from the environment -
/// appsettings.json ships blanks on purpose so a missing secret fails loudly at startup
/// instead of silently signing tokens with a committed default.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    public const int MinimumSigningKeyBytes = 32;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage =
        "A JWT signing key is required. Set Jwt__SigningKey to a random value of at least 32 bytes.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Forced-reset tokens are short-lived: they exist only to carry the user from the login
    /// response to the change-password call.
    /// </summary>
    [Range(1, 60)]
    public int PasswordChangeTokenLifetimeMinutes { get; set; } = 10;

    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;
}
