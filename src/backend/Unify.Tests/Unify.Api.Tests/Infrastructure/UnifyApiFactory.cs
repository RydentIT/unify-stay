using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Common;

namespace Unify.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API - real DI graph, real middleware, real endpoints - with two deliberate
/// substitutions: configuration comes from here rather than the environment, and the database
/// probe is stubbed so no Postgres instance is required.
/// </summary>
public sealed class UnifyApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Long enough to satisfy the 32-byte HS256 minimum. Test-only, never a real key.</summary>
    public const string TestSigningKey = "unify-stay-test-signing-key-not-a-real-secret-0123456789";

    public HealthState DatabaseState { get; set; } = HealthState.Healthy;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: that profile turns on startup migrations, which would need a real
        // database. This keeps the tests hermetic.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] =
                    "Host=localhost;Port=5432;Database=unify_test;Username=unify;Password=unify",
                ["Database:RunMigrationsOnStartup"] = "false",
                ["Jwt:Issuer"] = "https://unify.test",
                ["Jwt:Audience"] = "unify-stay-tests",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var probe = Substitute.For<IDatabaseHealthProbe>();
            probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(_ => DatabaseState);

            services.RemoveAll<IDatabaseHealthProbe>();
            services.AddSingleton(probe);
        });
    }
}
