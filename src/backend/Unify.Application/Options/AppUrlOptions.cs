namespace Unify.Application.Options;

/// <summary>
/// Front-end URLs that appear inside outbound emails. These point at the PORTAL, not the API:
/// the user clicks into a page that then calls the API, so the links must be configurable per
/// environment rather than derived from the API's own host.
/// </summary>
public sealed class AppUrlOptions
{
    public const string SectionName = "AppUrls";

    public string UserPortalBaseUrl { get; set; } = "http://localhost:3000";

    public string AdminPortalBaseUrl { get; set; } = "http://localhost:3001";

    /// <summary>
    /// Admin/Staff accounts are never self-registered on the user portal, so their
    /// verification link (bootstrap-admin, or a future admin-created-staff flow) has to point
    /// at the admin portal instead - it is the only one of the two that has a session that
    /// account could ever hold.
    /// </summary>
    public string BuildVerifyEmailUrl(string token, bool forAdminPortal = false) =>
        $"{Trim(forAdminPortal ? AdminPortalBaseUrl : UserPortalBaseUrl)}/verify-email/{Uri.EscapeDataString(token)}";

    public string BuildResetPasswordUrl(string token) =>
        $"{Trim(UserPortalBaseUrl)}/reset-password/{Uri.EscapeDataString(token)}";

    public string BuildConfirmEmailChangeUrl(string token) =>
        $"{Trim(UserPortalBaseUrl)}/profile/change-email/{Uri.EscapeDataString(token)}";

    public string BuildLoginUrl() => $"{Trim(UserPortalBaseUrl)}/login";

    private static string Trim(string url) => url.TrimEnd('/');
}
