using FluentValidation;
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
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// The mandatory reset a user completes while holding a limited-scope token (LOG-014).
/// This is the ONE endpoint that credential accepts, which is why it lives beside login rather
/// than under Profile - the caller is not yet a fully authenticated user.
/// </summary>
public sealed record ChangePasswordForcedCommand(string NewPassword) : ICommand<Result<LoginResult>>;

public sealed class ChangePasswordForcedCommandValidator : AbstractValidator<ChangePasswordForcedCommand>
{
    public ChangePasswordForcedCommandValidator(IOptions<AuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(options.Value.MinimumPasswordLength)
                .WithMessage($"Password must be at least {options.Value.MinimumPasswordLength} characters long.")
            .MaximumLength(256);
    }
}

internal sealed class ChangePasswordForcedCommandHandler
    : ICommandHandler<ChangePasswordForcedCommand, Result<LoginResult>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessions;
    private readonly LoginSessionIssuer _sessionIssuer;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ChangePasswordForcedCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ISessionRepository sessions,
        LoginSessionIssuer sessionIssuer,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _sessionIssuer = sessionIssuer;
        _emailSender = emailSender;
        _templates = templates;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<LoginResult>> HandleAsync(
        ChangePasswordForcedCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        User? user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return Result.Failure<LoginResult>(AuthErrors.UserNotFound);
        }

        // The new password must not simply restore the one being forced out.
        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return Result.Failure<LoginResult>(AuthErrors.PasswordMatchesCurrent);
        }

        DateTimeOffset now = _clock.UtcNow;

        // SetPassword clears must_change_password (LOG-014), which is what lets the reissued
        // token below come back with full scope.
        user.SetPassword(_passwordHasher.Hash(request.NewPassword), now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        // The credential that got us here was issued under the old password; nothing from before
        // the reset should survive it.
        await _sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.ForcedPasswordChanged,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken).ConfigureAwait(false);

        await _emailSender.SendAsync(
            _templates.BuildPasswordChangedEmail(user.Email.Value, user.FullName),
            cancellationToken).ConfigureAwait(false);

        // LOG-014: hand back a full-access token so the user continues without a second sign-in.
        LoginResult result = await _sessionIssuer
            .IssueAsync(user, rememberMe: false, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(result);
    }
}
