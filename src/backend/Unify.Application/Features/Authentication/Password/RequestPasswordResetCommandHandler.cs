using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Options;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Password;

/// <summary>
/// FPW-003 / BR-FPW-002 / FPW-009.
///
/// This handler returns <see cref="Result.Success"/> on EVERY path, including unknown
/// addresses. That is the requirement, not an oversight: any observable difference between
/// "we sent a link" and "no such account" turns this endpoint into an account-existence oracle.
///
/// What varies is only what lands in the inbox:
///   * password account -> a reset link.
///   * Google-only      -> a message pointing at Google sign-in (FPW-009). No reset token is
///                         created, because there is no password to reset.
///   * no account       -> nothing at all.
/// </summary>
internal sealed class RequestPasswordResetCommandHandler : ICommandHandler<RequestPasswordResetCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _tokens;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly AuthOptions _auth;
    private readonly AppUrlOptions _urls;

    public RequestPasswordResetCommandHandler(
        IUserRepository users,
        IPasswordResetTokenRepository tokens,
        ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
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
        _currentUser = currentUser;
        _clock = clock;
        _auth = auth.Value;
        _urls = urls.Value;
    }

    public async Task<Result> HandleAsync(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        User? user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.PasswordResetRequested,
                UserId = user?.Id,
                Email = email.Value,
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { accountExists = user is not null },
            },
            cancellationToken).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return Result.Success();
        }

        DateTimeOffset now = _clock.UtcNow;

        // FPW-009: Google-only accounts have no password hash. Sending a reset link would lead
        // to a dead end, so they get directed to the Google button instead - and crucially, no
        // token is minted for an account that cannot use one.
        if (!user.HasPassword)
        {
            await _emailSender.SendAsync(
                _templates.BuildPasswordResetForGoogleAccountEmail(
                    user.Email.Value,
                    user.FullName,
                    _urls.BuildLoginUrl()),
                cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        // Only the newest link should work.
        await _tokens.InvalidateAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        GeneratedToken generated = _tokenGenerator.Generate();

        var token = new PasswordResetToken(
            Guid.CreateVersion7(),
            user.Id,
            generated.Hash,
            now.AddMinutes(_auth.PasswordResetTokenLifetimeMinutes),
            now);

        await _tokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildPasswordResetEmail(
                user.Email.Value,
                user.FullName,
                _urls.BuildResetPasswordUrl(generated.RawValue)),
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
