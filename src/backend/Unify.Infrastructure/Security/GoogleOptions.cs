using System.ComponentModel.DataAnnotations;

namespace Unify.Infrastructure.Security;

/// <summary>
/// Bound from the "Google" section. ClientId is intentionally allowed to be empty so the app
/// boots without Google configured; the validator reports a clear error instead of the endpoint
/// failing obscurely at request time.
/// </summary>
public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;

    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
