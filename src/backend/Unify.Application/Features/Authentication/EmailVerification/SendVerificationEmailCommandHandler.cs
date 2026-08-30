using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Options;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.EmailVerification;

/// <summary>
/// EVR-001 and EVR-005/BR-EVR-002: every issue invalidates any outstanding token first, so only
/// the most recent link in the user's inbox works. Without that, an old link that leaked stays
/// usable for its full lifetime.
/// </summary>
internal sealed class SendVerificationEmailCommandHandler : ICommandHandler<SendVerificationEmailCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IEmailVerificationTokenRepository _tokens;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly IDateTimeProvider _clock;
    private readonly AuthOptions _auth;
    private readonly AppUrlOptions _urls;

    public SendVerificationEmailCommandHandler(
        IUserRepository users,
        IEmailVerificationTokenRepository tokens,
        ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        IDateTimeProvider clock,
        IOptions<AuthOptions> auth,
        IOptions<AppUrlOptions> urls)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(urls);

        _users = users;
        _tokens = tokens;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _clock = clock;
        _auth = auth.Value;
        _urls = urls.Value;
    }

    public async Task<Result> HandleAsync(SendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure(AuthErrors.UserNotFound);
        }

        if (user.EmailVerified)
        {
            return Result.Failure(AuthErrors.AlreadyVerified);
        }

        DateTimeOffset now = _clock.UtcNow;

        // BR-EVR-002: retire anything still outstanding before minting a replacement.
        await _tokens.InvalidateAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        GeneratedToken generated = _tokenGenerator.Generate();

        var token = new EmailVerificationToken(
            Guid.CreateVersion7(),
            user.Id,
            generated.Hash,
            now.AddHours(_auth.EmailVerificationTokenLifetimeHours),
            now);

        await _tokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

        // Admin/Staff accounts (bootstrap-admin, or a future admin-created-staff flow) have no
        // session on the user portal to land in - only the admin portal is ever reachable for
        // them, so their link has to point there instead.
        bool forAdminPortal = user.Roles.Any(role => role is RoleName.Admin or RoleName.Staff);

        // Only the raw value leaves the process, and only into this one email.
        string verificationUrl = _urls.BuildVerifyEmailUrl(generated.RawValue, forAdminPortal);

        await _emailSender.SendAsync(
            _templates.BuildVerificationEmail(user.Email.Value, user.FullName, verificationUrl),
            cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.VerificationEmailSent,
                UserId = user.Id,
                Email = user.Email.Value,
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
