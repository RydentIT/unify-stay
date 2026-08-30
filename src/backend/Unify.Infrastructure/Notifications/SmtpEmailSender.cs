using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Unify.Application.Abstractions.Notifications;

namespace Unify.Infrastructure.Notifications;

/// <summary>
/// Real SMTP delivery through MailKit. Configured entirely from EmailOptions, so pointing it at
/// a local catcher (MailHog on 1025) or a production relay is a config change only.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody ?? StripHtml(message.HtmlBody),
        }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            SecureSocketOptions socketOptions = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socketOptions, cancellationToken)
                .ConfigureAwait(false);

            // A local catcher accepts anonymous mail; only authenticate when told to.
            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
            {
                await client
                    .AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Sent {Subject} to {Recipient}.", message.Subject, message.To);
    }

    /// <summary>Crude fallback so a text part always exists; templates supply their own where it matters.</summary>
    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
}
