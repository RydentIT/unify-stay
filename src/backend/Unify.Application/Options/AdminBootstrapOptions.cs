namespace Unify.Application.Options;

/// <summary>
/// Bound directly from the ADMIN_BOOTSTRAP_EMAIL / ADMIN_BOOTSTRAP_PASSWORD environment
/// variables (not nested under a configuration section - see Infrastructure's
/// DependencyInjection for the binding). Read only during the explicit `bootstrap-admin` CLI
/// invocation; never used by the running web host.
/// </summary>
public sealed class AdminBootstrapOptions
{
    public string? Email { get; set; }

    public string? Password { get; set; }
}
