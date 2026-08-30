namespace Unify.Application.Abstractions.Notifications;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);

/// <summary>
/// Outbound transactional email. Implementations are selected by configuration: a log-based
/// sender for local dev, MailKit SMTP otherwise.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the messages the six modules send. Kept as an abstraction rather than inline string
/// building in each handler so that handlers stay about business rules, and so the whole set of
/// outbound wording can be reviewed and later templated in one place.
///
/// Bodies are plain/simple HTML for the MVP - no design work, per the current UI deprioritisation.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>Verification link sent immediately after email registration (EVR-001).</summary>
    EmailMessage BuildVerificationEmail(string to, string recipientName, string verificationUrl);

    /// <summary>Welcome message for accounts created through Google, which skip verification.</summary>
    EmailMessage BuildGoogleWelcomeEmail(string to, string recipientName);

    /// <summary>
    /// Sent when a Google identity is linked onto an existing account (REG-004). The user did
    /// not necessarily initiate this, so it doubles as a security notice.
    /// </summary>
    EmailMessage BuildAccountLinkedEmail(string to, string recipientName, string provider);

    EmailMessage BuildPasswordResetEmail(string to, string recipientName, string resetUrl);

    /// <summary>
    /// Sent to a Google-only account that asked for a password reset (FPW-009). Externally the
    /// request looks identical to any other; this message is what actually differs.
    /// </summary>
    EmailMessage BuildPasswordResetForGoogleAccountEmail(string to, string recipientName, string loginUrl);

    EmailMessage BuildPasswordChangedEmail(string to, string recipientName);

    EmailMessage BuildSecurityAlertEmail(string to, string recipientName, string detail);

    /// <summary>Sent when the lockout threshold trips (LOG-006).</summary>
    EmailMessage BuildAccountLockedEmail(string to, string recipientName, DateTimeOffset lockedUntil);

    EmailMessage BuildProfileChangedEmail(string to, string recipientName, string fieldsChanged);

    /// <summary>Confirmation link sent to the NEW address during an email change (PRF-009).</summary>
    EmailMessage BuildEmailChangeConfirmationEmail(string to, string recipientName, string confirmationUrl);

    /// <summary>Notice sent to the OLD address so a hijacked change cannot happen silently.</summary>
    EmailMessage BuildEmailChangeNoticeEmail(string to, string recipientName, string newEmail);

    EmailMessage BuildUpgradeApprovedEmail(string to, string recipientName);

    EmailMessage BuildUpgradeRejectedEmail(string to, string recipientName, string reason);

    EmailMessage BuildAccountDeactivatedEmail(string to, string recipientName);

    EmailMessage BuildAccountDeletedEmail(string to, string recipientName);

    /// <summary>Sent when an admin lifts a suspension (Part 4). No corresponding "you were
    /// suspended" email - the account is blocked at login, where the message is shown directly.</summary>
    EmailMessage BuildSuspensionLiftedEmail(string to, string recipientName);
}
