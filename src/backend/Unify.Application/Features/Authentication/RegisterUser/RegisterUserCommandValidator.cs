using FluentValidation;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength)
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(12).WithMessage("Password must be at least 12 characters long.")
            .MaximumLength(256);

        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.AcceptedTerms)
            .Equal(true).WithMessage("The terms of service must be accepted.");
    }
}
