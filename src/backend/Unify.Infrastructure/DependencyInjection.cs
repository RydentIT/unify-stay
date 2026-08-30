using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Storage;
using Unify.Application.Abstractions.Time;
using Unify.Application.Options;
using Unify.Infrastructure.Auditing;
using Unify.Infrastructure.Migrations;
using Unify.Infrastructure.Notifications;
using Unify.Infrastructure.Persistence;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Security;
using Unify.Infrastructure.Storage;
using Unify.Infrastructure.Time;

namespace Unify.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Binds configuration and registers a concrete implementation for every port declared in
    /// Unify.Application. Options are validated on start, so a missing connection string or a
    /// signing key that is too short fails the host immediately rather than on first request.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);

        // One data source per process owns the connection pool.
        services.TryAddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.TryAddScoped<IUserRepository, UserRepository>();
        services.TryAddScoped<ISessionRepository, SessionRepository>();
        services.TryAddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.TryAddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.TryAddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.TryAddScoped<IPendingEmailChangeRepository, PendingEmailChangeRepository>();
        services.TryAddScoped<IRoleUpgradeRequestRepository, RoleUpgradeRequestRepository>();
        services.TryAddScoped<IUserSuspensionRepository, UserSuspensionRepository>();
        services.TryAddScoped<IAuditLogRepository, AuditLogRepository>();
        services.TryAddScoped<IDatabaseHealthProbe, PostgresDatabaseHealthProbe>();
        services.TryAddScoped<IAuditLogger, AuditLogger>();

        services.TryAddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.TryAddSingleton<ITokenService, JwtTokenService>();
        services.TryAddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.TryAddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.TryAddSingleton<IFileStorageService, LocalFileStorageService>();
        services.TryAddSingleton<IEmailTemplateService, EmailTemplateService>();

        AddEmailSender(services, configuration);

        services.TryAddSingleton<DatabaseMigrator>();

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Configure(options =>
            {
                // ConnectionStrings__Unify is the conventional env var, so honour it as an alias
                // rather than requiring both spellings.
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

        // Deliberately NOT ValidateOnStart: Google is optional, and an unconfigured client id
        // must not stop the whole API from booting. The validator reports it per request.
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<AppUrlOptions>(configuration.GetSection(AppUrlOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        // Flat env var names (ADMIN_BOOTSTRAP_EMAIL / ADMIN_BOOTSTRAP_PASSWORD), not a nested
        // section - these are read only by the explicit `bootstrap-admin` CLI command and are
        // deliberately named to stand out from the rest of the ASP.NET Core configuration keys.
        services.Configure<AdminBootstrapOptions>(options =>
        {
            options.Email = configuration["ADMIN_BOOTSTRAP_EMAIL"];
            options.Password = configuration["ADMIN_BOOTSTRAP_PASSWORD"];
        });
    }

    /// <summary>
    /// Chooses the sender from configuration. Swapping providers is a config change; the whole
    /// application depends only on IEmailSender.
    /// </summary>
    private static void AddEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (emailOptions.UsesSmtp)
        {
            services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.TryAddSingleton<IEmailSender, LoggingEmailSender>();
        }
    }
}
