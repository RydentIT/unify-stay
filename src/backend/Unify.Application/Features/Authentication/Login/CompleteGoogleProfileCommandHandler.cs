using System.Text.RegularExpressions;
using FluentValidation;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// Supplies the phone number a Google-registered account could not give at signup, while
/// holding a limited-scope token (mirrors <see cref="ChangePasswordForcedCommandHandler"/>).
/// This is the ONE endpoint that credential accepts, which is why it lives beside login rather
/// than under Profile - the caller is not yet a fully authenticated user.
/// </summary>
public sealed record CompleteGoogleProfileCommand(string ContactNumber) : ICommand<Result<LoginResult>>;

public sealed partial class CompleteGoogleProfileCommandValidator : AbstractValidator<CompleteGoogleProfileCommand>
{
    [GeneratedRegex(@"^\+?[0-9()\-\s]{7,32}$")]
    private static partial Regex ContactNumberPattern();

    public CompleteGoogleProfileCommandValidator()
    {
        RuleFor(command => command.ContactNumber)
            .NotEmpty().WithMessage("A phone number is required.")
            .MaximumLength(32)
            .Matches(ContactNumberPattern()).WithMessage("Enter a valid phone number.");
    }
}

internal sealed class CompleteGoogleProfileCommandHandler
    : ICommandHandler<CompleteGoogleProfileCommand, Result<LoginResult>>
{
    private readonly IUserRepository _users;
    private readonly LoginSessionIssuer _sessionIssuer;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public CompleteGoogleProfileCommandHandler(
        IUserRepository users,
        LoginSessionIssuer sessionIssuer,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _users = users;
        _sessionIssuer = sessionIssuer;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<LoginResult>> HandleAsync(
        CompleteGoogleProfileCommand request,
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

        DateTimeOffset now = _clock.UtcNow;

        // CompleteProfile clears must_complete_profile, which is what lets the reissued token
        // below come back with full scope.
        user.CompleteProfile(request.ContactNumber, now);
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.ProfileCompleted,
                UserId = user.Id,
                Email = user.Email.Value,
                IpAddress = _currentUser.IpAddress,
            },
            cancellationToken).ConfigureAwait(false);

        // Hand back a full-access token so the user continues without a second sign-in.
        LoginResult result = await _sessionIssuer
            .IssueAsync(user, rememberMe: false, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(result);
    }
}
