using FluentValidation;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.EmailVerification;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator() =>
        RuleFor(command => command.Token).NotEmpty().MaximumLength(512);
}

public sealed class ResendVerificationEmailCommandValidator
    : AbstractValidator<ResendVerificationEmailCommand>
{
    public ResendVerificationEmailCommandValidator() =>
        RuleFor(command => command.Email).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
}
