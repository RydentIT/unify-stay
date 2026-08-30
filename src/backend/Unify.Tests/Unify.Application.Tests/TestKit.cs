using Microsoft.Extensions.Options;
using NSubstitute;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Storage;
using Unify.Application.Abstractions.Time;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Tests;

/// <summary>
/// Shared substitutes and builders. Every handler under test takes a wide constructor, so
/// assembling them by hand in each test would bury the rule being exercised in setup noise.
/// </summary>
internal sealed class TestKit
{
    public static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    public IUserRepository Users { get; } = Substitute.For<IUserRepository>();

    public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();

    public ILoginAttemptRepository LoginAttempts { get; } = Substitute.For<ILoginAttemptRepository>();

    public IPasswordResetTokenRepository ResetTokens { get; } = Substitute.For<IPasswordResetTokenRepository>();

    public IEmailVerificationTokenRepository VerificationTokens { get; } =
        Substitute.For<IEmailVerificationTokenRepository>();

    public IPendingEmailChangeRepository PendingEmailChanges { get; } =
        Substitute.For<IPendingEmailChangeRepository>();

    public IRoleUpgradeRequestRepository UpgradeRequests { get; } =
        Substitute.For<IRoleUpgradeRequestRepository>();

    public IUserSuspensionRepository Suspensions { get; } = Substitute.For<IUserSuspensionRepository>();

    public IPasswordHasher PasswordHasher { get; } = Substitute.For<IPasswordHasher>();

    public ITokenService TokenService { get; } = Substitute.For<ITokenService>();

    public ISecureTokenGenerator TokenGenerator { get; } = Substitute.For<ISecureTokenGenerator>();

    public IGoogleTokenValidator GoogleValidator { get; } = Substitute.For<IGoogleTokenValidator>();

    public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

    public IEmailTemplateService Templates { get; } = Substitute.For<IEmailTemplateService>();

    public IFileStorageService FileStorage { get; } = Substitute.For<IFileStorageService>();

    public IAuditLogger AuditLogger { get; } = Substitute.For<IAuditLogger>();

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IDateTimeProvider Clock { get; } = Substitute.For<IDateTimeProvider>();

    public IDispatcher Dispatcher { get; } = Substitute.For<IDispatcher>();

    public AuthOptions Auth { get; } = new();

    public AppUrlOptions Urls { get; } = new();

    public TestKit()
    {
        Clock.UtcNow.Returns(Now);

        // Sensible neutral defaults; individual tests override what they care about.
        TokenGenerator.Generate().Returns(new GeneratedToken("raw-token", "hashed-token"));
        TokenGenerator.Hash(Arg.Any<string>()).Returns(callInfo => $"hash:{callInfo.Arg<string>()}");

        TokenService
            .CreateAccessToken(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<RoleName>>(),
                Arg.Any<TokenScope>())
            .Returns(callInfo => new AccessToken(
                "access-token",
                Now.AddHours(1),
                callInfo.ArgAt<TokenScope>(3)));

        Templates
            .BuildVerificationEmail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new EmailMessage("to@example.com", "subject", "body"));
    }

    public IOptions<AuthOptions> AuthOptions => Microsoft.Extensions.Options.Options.Create(Auth);

    public IOptions<AppUrlOptions> UrlOptions => Microsoft.Extensions.Options.Options.Create(Urls);

    /// <summary>Signs the given user in for handlers that read ICurrentUser.</summary>
    public void SignIn(User user, TokenScope scope = TokenScope.Full, Guid? sessionId = null)
    {
        CurrentUser.UserId.Returns(user.Id);
        CurrentUser.Email.Returns(user.Email.Value);
        CurrentUser.IsAuthenticated.Returns(true);
        CurrentUser.Scope.Returns(scope);
        CurrentUser.Roles.Returns(user.Roles);
        CurrentUser.SessionId.Returns(sessionId);
        CurrentUser.IpAddress.Returns("203.0.113.10");

        Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
    }

    public static User ActiveStudent(
        string email = "student@example.com",
        string? passwordHash = "hashed-password",
        bool emailVerified = true,
        bool mustChangePassword = false,
        UserStatus status = UserStatus.Active,
        string? contactNumber = null)
    {
        var user = new User(
            Guid.CreateVersion7(),
            "Test",
            "Student",
            Email.Create(email),
            passwordHash,
            emailVerified,
            mustChangePassword,
            status,
            Now.AddDays(-30),
            Now.AddDays(-30),
            contactNumber: contactNumber);

        user.AddRole(RoleName.Student);
        user.AddAuthProvider(AuthProvider.Local(Guid.CreateVersion7(), user.Id, Now.AddDays(-30)));

        return user;
    }

    /// <summary>A Google-only account: no password hash, so password flows must refuse it.</summary>
    public static User GoogleOnlyUser(string email = "google@example.com", string subject = "google-subject-1")
    {
        var user = new User(
            Guid.CreateVersion7(),
            "Google",
            "User",
            Email.Create(email),
            passwordHash: null,
            emailVerified: true,
            mustChangePassword: false,
            status: UserStatus.Active,
            createdAt: Now.AddDays(-10),
            updatedAt: Now.AddDays(-10));

        user.AddRole(RoleName.Student);
        user.AddAuthProvider(AuthProvider.Google(Guid.CreateVersion7(), user.Id, subject, true, Now.AddDays(-10)));

        return user;
    }
}
