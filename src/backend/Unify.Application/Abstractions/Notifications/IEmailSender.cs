namespace Unify.Application.Abstractions.Notifications;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);

/// <summary>
/// Outbound transactional email (verification, password reset). The concrete provider is
/// deliberately unchosen: Infrastructure ships a logging implementation for local dev, and a
/// real provider gets wired once the cloud target is picked.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
