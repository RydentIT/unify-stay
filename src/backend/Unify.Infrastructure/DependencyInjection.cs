using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Infrastructure.Auditing;
using Unify.Infrastructure.Migrations;
using Unify.Infrastructure.Notifications;
using Unify.Infrastructure.Persistence;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Security;
using Unify.Infrastructure.Time;

namespace Unify.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Binds configuration and registers a concrete implementation for every port declared in
    /// Unify.Application. Options are validated on start, so a missing connection string or
    /// signing key fails the host immediately rather than on first request.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Configure(options =>
            {
                // ConnectionStrings__Unify is the conventional env var, so honour it as an
                // alias for Database__ConnectionString rather than requiring both.
                string? fromConnectionStrings = configuration.GetConnectionString("Unify");

                if (string.IsNullOrWhiteSpace(options.ConnectionString) &&
                    !string.IsNullOrWhiteSpace(fromConnectionStrings))
                {
                    options.ConnectionString = fromConnectionStrings;
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // One data source per process owns the connection pool.
        services.TryAddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.TryAddScoped<IUserRepository, UserRepository>();
        services.TryAddScoped<IAuditLogRepository, AuditLogRepository>();
        services.TryAddScoped<IDatabaseHealthProbe, PostgresDatabaseHealthProbe>();
        services.TryAddScoped<IAuditLogger, AuditLogger>();

        services.TryAddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.TryAddSingleton<ITokenService, JwtTokenService>();
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Local-dev stand-in. Replace this single registration when a mail provider is chosen.
        services.TryAddSingleton<IEmailSender, LoggingEmailSender>();

        services.TryAddSingleton<DatabaseMigrator>();

        return services;
    }
}
