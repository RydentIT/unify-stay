using FluentValidation.Results;
using Unify.Application.Features.Authentication.RegisterUser;

namespace Unify.Application.Tests.Features.Authentication;

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    private static RegisterUserCommand Valid() =>
        new("someone@example.com", "correct-horse-battery-staple", "Someone", true);

    [Fact]
    public void Accepts_a_well_formed_command()
    {
        ValidationResult result = _validator.Validate(Valid());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    public void Rejects_a_malformed_email(string email)
    {
        ValidationResult result = _validator.Validate(Valid() with { Email = email });

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(RegisterUserCommand.Email));
    }

    [Fact]
    public void Rejects_a_password_below_the_minimum_length()
    {
        ValidationResult result = _validator.Validate(Valid() with { Password = new string('a', 11) });

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(RegisterUserCommand.Password));
    }

    [Fact]
    public void Requires_the_terms_to_be_accepted()
    {
        ValidationResult result = _validator.Validate(Valid() with { AcceptedTerms = false });

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(RegisterUserCommand.AcceptedTerms));
    }
}
