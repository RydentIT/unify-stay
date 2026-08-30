namespace Unify.Infrastructure.Notifications;

/// <summary>
/// Bound from the "Email" section. Credentials arrive from the environment only - nothing
/// sensitive is ever written into appsettings.json.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"log" writes messages to the logger; "smtp" sends through MailKit.</summary>
    public string Provider { get; set; } = "log";

    public string FromAddress { get; set; } = "no-reply@unifystay.local";

    public string FromName { get; set; } = "UnifyStay";

    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;

    /// <summary>
    /// Off by default because the local catcher (MailHog on 1025) speaks plain SMTP. Any real
    /// provider must set this true.
    /// </summary>
    public bool UseStartTls { get; set; }

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public bool UsesSmtp => string.Equals(Provider, "smtp", StringComparison.OrdinalIgnoreCase);
}
