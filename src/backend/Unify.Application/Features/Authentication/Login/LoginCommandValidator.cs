using FluentValidation;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

public sealed class LoginWithEmailCommandValidator : AbstractValidator<LoginWithEmailCommand>
{
    public LoginWithEmailCommandValidator()
    {
        // Shape only. Whether the credentials are correct is the handler's business, and every
        // one of its failure modes must look identical to the caller (BR-LOG-001) - so there is
        // deliberately no "email not found" style rule here.
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}

public sealed class LoginWithGoogleCommandValidator : AbstractValidator<LoginWithGoogleCommand>
{
    public LoginWithGoogleCommandValidator() =>
        RuleFor(command => command.IdToken).NotEmpty().MaximumLength(4096);
}
