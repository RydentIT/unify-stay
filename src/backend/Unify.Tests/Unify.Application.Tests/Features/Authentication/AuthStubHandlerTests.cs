using NSubstitute;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.Login;
using Unify.Application.Features.Authentication.RegisterUser;
using Unify.Application.Features.Authentication.RequestPasswordReset;

namespace Unify.Application.Tests.Features.Authentication;

/// <summary>
/// Pins the SCAFFOLD behaviour of the auth handlers: they are stubs that report
/// NotImplemented. These tests are expected to be rewritten - not deleted - as each module
/// lands, and until then they guarantee the endpoints cannot appear to work by accident.
/// </summary>
public sealed class AuthStubHandlerTests
{
    [Fact]
    public async Task Register_reports_not_implemented()
    {
        var handler = new RegisterUserCommandHandler(
            Substitute.For<IUserRepository>(),
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IAuditLogger>(),
            Substitute.For<IDateTimeProvider>());

        Result<RegisterUserResult> result = await handler.HandleAsync(
            new RegisterUserCommand("someone@example.com", "correct-horse-battery", "Someone", true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotImplemented, result.Error.Type);
    }

    [Fact]
    public async Task Login_reports_not_implemented()
    {
        var handler = new LoginCommandHandler(
            Substitute.For<IUserRepository>(),
            Substitute.For<IPasswordHasher>(),
            Substitute.For<ITokenService>(),
            Substitute.For<IAuditLogger>());

        Result<LoginResult> result = await handler.HandleAsync(
            new LoginCommand("someone@example.com", "correct-horse-battery"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotImplemented, result.Error.Type);
    }

    [Fact]
    public async Task Password_reset_reports_not_implemented()
    {
        var handler = new RequestPasswordResetCommandHandler(
            Substitute.For<IUserRepository>(),
            Substitute.For<IEmailSender>(),
            Substitute.For<IAuditLogger>(),
            Substitute.For<IDateTimeProvider>());

        Result result = await handler.HandleAsync(
            new RequestPasswordResetCommand("someone@example.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotImplemented, result.Error.Type);
    }
}
