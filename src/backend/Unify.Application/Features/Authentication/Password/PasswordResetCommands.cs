using FluentValidation;
using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Password;

/// <summary>
/// Starts the reset flow. Always reports success (FPW-003/BR-FPW-002) - see the handler.
/// </summary>
public sealed record RequestPasswordResetCommand(string Email) : ICommand<Result>;

/// <summary>Completes the reset using the emailed token.</summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Result>;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator() =>
        RuleFor(command => command.Email).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IOptions<AuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(command => command.Token).NotEmpty().MaximumLength(512);

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(options.Value.MinimumPasswordLength)
                .WithMessage($"Password must be at least {options.Value.MinimumPasswordLength} characters long.")
            .MaximumLength(256);
    }
}
