using Microsoft.Extensions.Logging;
using Unify.Application.Abstractions.Notifications;

namespace Unify.Infrastructure.Notifications;

/// <summary>
/// Development stand-in that writes the message to the log instead of sending it. The real
/// provider is intentionally unchosen until the cloud target is settled; swapping it in means
/// registering a different IEmailSender in DependencyInjection and nothing else.
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "Email not sent (no provider configured). To: {Recipient} Subject: {Subject}",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }
}
