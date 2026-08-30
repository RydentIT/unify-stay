using System.Net;
using Unify.Application.Abstractions.Notifications;

namespace Unify.Infrastructure.Notifications;

/// <summary>
/// Builds every outbound message.
///
/// Simple HTML with a matching plain-text part, no design work - the UI is deliberately
/// deprioritised for the MVP. Everything user-supplied (names, reasons) is HTML-encoded on the
/// way in: these bodies are assembled by string concatenation, so an unencoded name would be an
/// injection point into someone else's inbox.
/// </summary>
internal sealed class EmailTemplateService : IEmailTemplateService
{
    public EmailMessage BuildVerificationEmail(string to, string recipientName, string verificationUrl) =>
        Build(
            to,
            "Verify your UnifyStay email address",
            recipientName,
            $"""
             <p>Thanks for signing up. Please confirm your email address to finish setting up your account.</p>
             <p><a href="{Encode(verificationUrl)}">Verify my email address</a></p>
             <p>If the link does not work, copy this into your browser:<br>{Encode(verificationUrl)}</p>
             <p>If you did not create a UnifyStay account, you can ignore this message.</p>
             """,
            $"Verify your email address: {verificationUrl}");

    public EmailMessage BuildGoogleWelcomeEmail(string to, string recipientName) =>
        Build(
            to,
            "Welcome to UnifyStay",
            recipientName,
            """
            <p>Your account is ready. You signed up with Google, so there is nothing else to confirm.</p>
            <p>Sign in any time using the <strong>Continue with Google</strong> button.</p>
            """,
            "Your UnifyStay account is ready. Sign in with Google.");

    public EmailMessage BuildAccountLinkedEmail(string to, string recipientName, string provider) =>
        Build(
            to,
            $"{provider} was linked to your UnifyStay account",
            recipientName,
            $"""
             <p>{Encode(provider)} sign-in has been linked to your existing UnifyStay account.
             You can now sign in either way.</p>
             <p><strong>If you did not do this, change your password immediately</strong> - it means
             someone else may control that {Encode(provider)} account.</p>
             """,
            $"{provider} sign-in was linked to your account. If this was not you, change your password immediately.");

    public EmailMessage BuildPasswordResetEmail(string to, string recipientName, string resetUrl) =>
        Build(
            to,
            "Reset your UnifyStay password",
            recipientName,
            $"""
             <p>We received a request to reset your password.</p>
             <p><a href="{Encode(resetUrl)}">Choose a new password</a></p>
             <p>If the link does not work, copy this into your browser:<br>{Encode(resetUrl)}</p>
             <p>This link can only be used once, and expires shortly. If you did not request it,
             nothing has changed and you can ignore this message.</p>
             """,
            $"Reset your password: {resetUrl}");

    /// <summary>
    /// FPW-009. The forgot-password endpoint answers identically for every address; this
    /// message is the only place the difference shows, and it only reaches the real owner.
    /// </summary>
    public EmailMessage BuildPasswordResetForGoogleAccountEmail(string to, string recipientName, string loginUrl) =>
        Build(
            to,
            "Signing in to UnifyStay",
            recipientName,
            $"""
             <p>Someone asked to reset the password for this address, but your account does not use
             a password - it signs in with Google.</p>
             <p><a href="{Encode(loginUrl)}">Go to sign in</a> and choose
             <strong>Continue with Google</strong>.</p>
             <p>If you cannot access your Google account, recover it with Google first.</p>
             """,
            $"Your account signs in with Google. Go to {loginUrl} and use Continue with Google.");

    public EmailMessage BuildPasswordChangedEmail(string to, string recipientName) =>
        Build(
            to,
            "Your UnifyStay password was changed",
            recipientName,
            """
            <p>Your password has just been changed, and you have been signed out on other devices.</p>
            <p><strong>If this was not you, reset your password now</strong> and contact support.</p>
            """,
            "Your password was changed. If this was not you, reset it immediately.");

    public EmailMessage BuildSecurityAlertEmail(string to, string recipientName, string detail) =>
        Build(
            to,
            "Security alert for your UnifyStay account",
            recipientName,
            $"<p>{Encode(detail)}</p><p>If this was not you, reset your password immediately.</p>",
            detail);

    public EmailMessage BuildAccountLockedEmail(string to, string recipientName, DateTimeOffset lockedUntil) =>
        Build(
            to,
            "Your UnifyStay account is temporarily locked",
            recipientName,
            $"""
             <p>There have been too many failed sign-in attempts, so the account is locked until
             <strong>{lockedUntil:u}</strong>.</p>
             <p>If that was you, wait and try again. If it was not, someone is guessing your
             password - reset it once the lock lifts.</p>
             """,
            $"Account locked after too many failed sign-in attempts. Try again after {lockedUntil:u}.");

    public EmailMessage BuildProfileChangedEmail(string to, string recipientName, string fieldsChanged) =>
        Build(
            to,
            "Your UnifyStay profile was updated",
            recipientName,
            $"<p>The following was updated on your profile: {Encode(fieldsChanged)}.</p>",
            $"Your profile was updated: {fieldsChanged}");

    public EmailMessage BuildEmailChangeConfirmationEmail(string to, string recipientName, string confirmationUrl) =>
        Build(
            to,
            "Confirm your new UnifyStay email address",
            recipientName,
            $"""
             <p>Confirm this address to finish moving your UnifyStay account to it.</p>
             <p><a href="{Encode(confirmationUrl)}">Confirm this email address</a></p>
             <p>Until you confirm, your account keeps using its current address.</p>
             """,
            $"Confirm your new email address: {confirmationUrl}");

    public EmailMessage BuildEmailChangeNoticeEmail(string to, string recipientName, string newEmail) =>
        Build(
            to,
            "A change of email address was requested",
            recipientName,
            $"""
             <p>Someone asked to move your UnifyStay account to <strong>{Encode(newEmail)}</strong>.
             Your current address stays in use until that new one is confirmed.</p>
             <p><strong>If you did not request this, change your password now</strong> - someone
             may have access to your account.</p>
             """,
            $"A change to {newEmail} was requested. If this was not you, change your password now.");

    public EmailMessage BuildUpgradeApprovedEmail(string to, string recipientName) =>
        Build(
            to,
            "Your Property Owner request was approved",
            recipientName,
            """
            <p>Your Property Owner upgrade has been approved and is active now.</p>
            <p>Sign out and back in if you do not see the new options straight away.</p>
            """,
            "Your Property Owner upgrade was approved.");

    public EmailMessage BuildUpgradeRejectedEmail(string to, string recipientName, string reason) =>
        Build(
            to,
            "Your Property Owner request was not approved",
            recipientName,
            $"""
             <p>Your Property Owner upgrade request was not approved.</p>
             <p><strong>Reason:</strong> {Encode(reason)}</p>
             <p>You can submit a new request with updated documents from your settings page.</p>
             """,
            $"Your upgrade request was not approved. Reason: {reason}");

    public EmailMessage BuildAccountDeactivatedEmail(string to, string recipientName) =>
        Build(
            to,
            "Your UnifyStay account is deactivated",
            recipientName,
            """
            <p>Your account has been deactivated and you have been signed out everywhere.</p>
            <p>You can reactivate it at any time simply by signing in again.</p>
            """,
            "Your account is deactivated. Sign in again to reactivate it.");

    public EmailMessage BuildAccountDeletedEmail(string to, string recipientName) =>
        Build(
            to,
            "Your UnifyStay account has been deleted",
            recipientName,
            """
            <p>Your account has been deleted and your personal details removed.</p>
            <p>This cannot be undone. You are welcome to register again at any time.</p>
            """,
            "Your account has been deleted. This cannot be undone.");

    public EmailMessage BuildSuspensionLiftedEmail(string to, string recipientName) =>
        Build(
            to,
            "Your UnifyStay account suspension has been lifted",
            recipientName,
            """
            <p>Your account suspension has been lifted. You can sign in again as normal.</p>
            """,
            "Your account suspension has been lifted. You can sign in again.");

    private static EmailMessage Build(
        string to,
        string subject,
        string recipientName,
        string bodyHtml,
        string textSummary)
    {
        string greetingName = string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName;

        string html = $"""
            <p>Hi {Encode(greetingName)},</p>
            {bodyHtml}
            <p>- The UnifyStay team</p>
            """;

        string text = $"Hi {greetingName},\n\n{textSummary}\n\n- The UnifyStay team";

        return new EmailMessage(to, subject, html, text);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
