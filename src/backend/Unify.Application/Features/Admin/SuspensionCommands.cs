using FluentValidation;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Admin;

/// <summary>
/// Part 4. "Suspended" = "banned" (product decision): imposing one rejects the target at login
/// outright and revokes every session they currently hold - there is no login-through or
/// Suspension Info experience.
/// </summary>
public sealed record SuspendUserCommand(Guid UserId, string Reason) : ICommand<Result>;

public sealed record LiftSuspensionCommand(Guid UserId) : ICommand<Result>;

public sealed class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("A reason is required to suspend an account.")
            .MaximumLength(255);
    }
}

public sealed class LiftSuspensionCommandValidator : AbstractValidator<LiftSuspensionCommand>
{
    public LiftSuspensionCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}
