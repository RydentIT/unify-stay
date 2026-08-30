using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Register;

public sealed partial class RegisterWithEmailCommandValidator : AbstractValidator<RegisterWithEmailCommand>
{
    /// <summary>
    /// Deliberately permissive: an optional leading +, then digits/spaces/hyphens/parentheses,
    /// with at least 7 digits overall. International formats vary too much for anything
    /// stricter to be worth the false rejections it would cause.
    /// </summary>
    [GeneratedRegex(@"^\+?[0-9()\-\s]{7,32}$")]
    private static partial Regex ContactNumberPattern();

    public RegisterWithEmailCommandValidator(IOptions<AuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        AuthOptions auth = options.Value;

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(60);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(60);

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength)
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(auth.MinimumPasswordLength)
                .WithMessage($"Password must be at least {auth.MinimumPasswordLength} characters long.")
            .MaximumLength(256);

        RuleFor(command => command.ContactNumber)
            .NotEmpty().WithMessage("A phone number is required.")
            .MaximumLength(32)
            .Matches(ContactNumberPattern()).WithMessage("Enter a valid phone number.");

        // REG-006. Enforced here so the account is never created without consent recorded.
        RuleFor(command => command.AcceptedTerms)
            .Equal(true)
            .WithMessage("The terms of service must be accepted.");
    }
}
