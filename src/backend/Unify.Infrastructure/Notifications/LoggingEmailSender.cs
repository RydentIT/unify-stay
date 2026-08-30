using Microsoft.Extensions.Logging;
using Unify.Application.Abstractions.Notifications;

namespace Unify.Infrastructure.Notifications;

/// <summary>
/// Writes the message to the log instead of sending it.
///
/// The body is logged in full and on purpose: during local development the verification and
/// reset links are only obtainable this way, so truncating them would make the flows untestable
/// without a mail catcher. This sender must never be selected outside development.
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "EMAIL (not sent - logging provider)\nTo: {Recipient}\nSubject: {Subject}\n{Body}",
            message.To,
            message.Subject,
            message.TextBody ?? message.HtmlBody);

        return Task.CompletedTask;
    }
}
