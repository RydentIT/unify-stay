using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Domain.Common;

namespace Unify.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API - real DI graph, real middleware, real endpoints, real validation and
/// real authorization policies - against substituted outbound ports.
///
/// The repositories are substituted rather than pointed at a database because these tests are
/// about the HTTP surface: routing, model binding, problem-details shape and the policy that
/// gates limited-scope tokens. The SQL behind those ports has its own integration suite.
/// </summary>
public sealed class UnifyApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Long enough for the 32-byte HS256 minimum. Test-only, never a real key.</summary>
    public const string TestSigningKey = "unify-stay-test-signing-key-not-a-real-secret-0123456789";

    public HealthState DatabaseState { get; set; } = HealthState.Healthy;

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

    public IAuditLogRepository AuditLogs { get; } = Substitute.For<IAuditLogRepository>();

    public IPasswordHasher PasswordHasher { get; } = Substitute.For<IPasswordHasher>();

    public IGoogleTokenValidator GoogleValidator { get; } = Substitute.For<IGoogleTokenValidator>();

    public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: that profile turns on startup migrations, which would need a real
        // database. This keeps the tests hermetic.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] =
                    "Host=localhost;Port=5432;Database=unify_test;Username=unify;Password=unify",
                ["Database:RunMigrationsOnStartup"] = "false",
                ["Jwt:Issuer"] = "https://unify.test",
                ["Jwt:Audience"] = "unify-stay-tests",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",

                // Generous, so the API-edge limiter does not mask the behaviour under test.
                // The lockout rules have their own dedicated coverage in the Application suite.
                ["RateLimiting:Registration:PermitLimit"] = "1000",
                ["RateLimiting:Login:PermitLimit"] = "1000",
                ["RateLimiting:PasswordReset:PermitLimit"] = "1000",
                ["RateLimiting:ResendVerification:PermitLimit"] = "1000",
                ["RateLimiting:Global:PermitLimit"] = "10000",
            }));

        builder.ConfigureTestServices(services =>
        {
            var probe = Substitute.For<IDatabaseHealthProbe>();
            probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(_ => DatabaseState);

            Replace(services, probe);
            Replace(services, Users);
            Replace(services, Sessions);
            Replace(services, LoginAttempts);
            Replace(services, ResetTokens);
            Replace(services, VerificationTokens);
            Replace(services, PendingEmailChanges);
            Replace(services, UpgradeRequests);
            Replace(services, AuditLogs);
            Replace(services, PasswordHasher);
            Replace(services, GoogleValidator);
            Replace(services, EmailSender);
        });
    }

    private static void Replace<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(instance);
    }
}
