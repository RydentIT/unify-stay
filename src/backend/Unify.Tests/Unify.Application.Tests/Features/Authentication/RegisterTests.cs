using NSubstitute;
using Unify.Application.Abstractions.Security;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.Register;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Authentication;

/// <summary>REG-003, REG-004, REG-005, REG-007 and BR-REG-004.</summary>
public sealed class RegisterTests
{
    private readonly TestKit _kit = new();

    private RegisterWithEmailCommandHandler CreateEmailHandler() => new(
        _kit.Users,
        _kit.PasswordHasher,
        _kit.Dispatcher,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock);

    private RegisterWithGoogleCommandHandler CreateGoogleHandler() => new(
        _kit.GoogleValidator,
        _kit.Users,
        _kit.EmailSender,
        _kit.Templates,
        _kit.AuditLogger,
        _kit.CurrentUser,
        _kit.Clock);

    [Fact]
    public async Task Creates_an_unverified_account_with_the_default_student_role()
    {
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _kit.PasswordHasher.Hash(Arg.Any<string>()).Returns("hashed");

        User? saved = null;
        await _kit.Users.AddAsync(Arg.Do<User>(user => saved = user), Arg.Any<CancellationToken>());

        Result<RegisterResult> result = await CreateEmailHandler().HandleAsync(
            new RegisterWithEmailCommand("New", "Student", "new@example.com", "correct-horse-battery", "+94711234567", true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.EmailVerificationRequired);

        Assert.NotNull(saved);
        Assert.False(saved.EmailVerified);
        Assert.Equal(RoleName.Student, Assert.Single(saved.Roles));
        Assert.True(saved.HasPassword);
    }

    /// <summary>REG-003: uniqueness spans providers, so a Google address cannot be re-claimed.</summary>
    [Fact]
    public async Task Rejects_an_address_already_registered_under_any_provider()
    {
        _kit.Users
            .GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(TestKit.GoogleOnlyUser("taken@example.com"));

        Result<RegisterResult> result = await CreateEmailHandler().HandleAsync(
            new RegisterWithEmailCommand("Someone", "Person", "taken@example.com", "correct-horse-battery", "+94711234567", true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyRegistered, result.Error);
        await _kit.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>BR-REG-004: Google already proved the address, so no verification email is sent.</summary>
    [Fact]
    public async Task Google_registration_skips_email_verification()
    {
        _kit.GoogleValidator
            .ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserInfo("google-sub", "gnew@example.com", EmailVerified: true, "G New", "G", "New"));

        _kit.Users.GetByProviderAsync(AuthProviderKind.Google, "google-sub", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        User? saved = null;
        await _kit.Users.AddAsync(Arg.Do<User>(user => saved = user), Arg.Any<CancellationToken>());

        Result<GoogleRegisterResult> result = await CreateGoogleHandler().HandleAsync(
            new RegisterWithGoogleCommand("id-token", true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.LinkedToExistingAccount);

        Assert.NotNull(saved);
        Assert.True(saved.EmailVerified);
        Assert.False(saved.HasPassword);

        await _kit.VerificationTokens.DidNotReceive()
            .AddAsync(Arg.Any<Domain.Authentication.EmailVerificationToken>(), Arg.Any<CancellationToken>());
    }

    /// <summary>REG-004: verified Google address collides with an existing account -> link it.</summary>
    [Fact]
    public async Task Links_google_to_an_existing_account_when_google_says_the_address_is_verified()
    {
        User existing = TestKit.ActiveStudent("shared@example.com");

        _kit.GoogleValidator
            .ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserInfo("google-sub", "shared@example.com", EmailVerified: true, "Shared Person", "Shared", "Person"));

        _kit.Users.GetByProviderAsync(AuthProviderKind.Google, "google-sub", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(existing);

        Result<GoogleRegisterResult> result = await CreateGoogleHandler().HandleAsync(
            new RegisterWithGoogleCommand("id-token", true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.LinkedToExistingAccount);
        Assert.Equal(existing.Id, result.Value.UserId);

        // Linked onto the SAME account - no duplicate was created.
        await _kit.Users.Received(1).AddAuthProviderAsync(
            Arg.Is<AuthProvider>(provider => provider.UserId == existing.Id && provider.Provider == AuthProviderKind.Google),
            Arg.Any<CancellationToken>());

        await _kit.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// REG-005: the same collision, but Google has NOT verified the address. Linking here would
    /// hand the existing account to whoever presented the token.
    /// </summary>
    [Fact]
    public async Task Refuses_to_link_when_google_has_not_verified_the_address()
    {
        User existing = TestKit.ActiveStudent("shared@example.com");

        _kit.GoogleValidator
            .ValidateAsync("id-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserInfo("google-sub", "shared@example.com", EmailVerified: false, "Shared Person", "Shared", "Person"));

        _kit.Users.GetByProviderAsync(AuthProviderKind.Google, "google-sub", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _kit.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(existing);

        Result<GoogleRegisterResult> result = await CreateGoogleHandler().HandleAsync(
            new RegisterWithGoogleCommand("id-token", true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UnverifiedGoogleEmailCollision, result.Error);

        await _kit.Users.DidNotReceive().AddAuthProviderAsync(Arg.Any<AuthProvider>(), Arg.Any<CancellationToken>());
        await _kit.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unverifiable_google_token()
    {
        _kit.GoogleValidator
            .ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GoogleUserInfo?)null);

        Result<GoogleRegisterResult> result = await CreateGoogleHandler().HandleAsync(
            new RegisterWithGoogleCommand("forged-token", true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidGoogleToken, result.Error);
    }
}
