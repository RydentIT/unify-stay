using FluentValidation;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Only shape is validated here. Whether the credentials are correct is decided by
        // the handler, and must produce one indistinguishable error for every failure mode.
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}
