using Microsoft.Extensions.Options;
using Unify.Api.Endpoints;
using Unify.Api.Extensions;
using Unify.Api.Identity;
using Unify.Api.Middleware;
using Unify.Application;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Features.Bootstrap;
using Unify.Infrastructure;
using Unify.Infrastructure.Migrations;
using Unify.Infrastructure.Persistence;

// Loads a .env file from the repository root into process environment variables, BEFORE the
// host builder reads them. Silent no-op if none is found (production, CI, containers all
// supply real environment variables directly and never ship a .env). .env is gitignored, so
// this is purely a local-dev convenience - walking up from the executable rather than assuming
// a fixed depth keeps it working whether run via `dotnet run`, the built DLL, or a test host.
string? repoRootEnvFile = FindRepoRootEnvFile(AppContext.BaseDirectory);

if (repoRootEnvFile is not null)
{
    DotNetEnv.Env.Load(repoRootEnvFile);
}

static string? FindRepoRootEnvFile(string startDirectory)
{
    DirectoryInfo? directory = new(startDirectory);

    while (directory is not null)
    {
        string candidate = Path.Combine(directory.FullName, ".env");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration precedence: appsettings.json (placeholders only) < appsettings.{Env}.json <
// user secrets in Development < environment variables (including anything loaded from .env
// above). Real secrets only ever arrive from the last two, which is why the committed files
// are blank.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddTransient<GlobalExceptionHandlingMiddleware>();

builder.Services.AddUnifyAuthentication();
builder.Services.AddUnifyRateLimiting(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The portals are served from different origins in every environment, so CORS is
// configuration-driven rather than hard-coded. An empty list means no browser origin is
// allowed, which is the correct default for a fresh environment.
string[] allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }
}));

WebApplication app = builder.Build();

// `dotnet run --project src/backend/Unify.Api -- migrate` applies pending migrations and
// exits. This is the path used outside Development, where startup migration is off; see
// scripts/migrate.sh and scripts/migrate.ps1.
if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    app.Services.GetRequiredService<DatabaseMigrator>().Migrate();
    return;
}

// `dotnet run --project src/backend/Unify.Api -- bootstrap-admin` creates the first Admin
// account and exits. Never runs implicitly - an operator has to invoke this on purpose. Safe
// to run more than once: it is a no-op once any Admin exists. See README for the required
// ADMIN_BOOTSTRAP_EMAIL / ADMIN_BOOTSTRAP_PASSWORD environment variables.
if (args.Contains("bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().RunAsync();
    return;
}

// Ordering matters: the exception handler wraps everything after it, so a failure in
// rate limiting, auth or an endpoint still produces a problem document.
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Swagger UI over the document MapOpenApi() already generates - no separate generator, so
    // there is exactly one description of the API, not two that can drift apart.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "UnifyStay API v1");
        options.RoutePrefix = "swagger";
    });

    DatabaseOptions databaseOptions = app.Services
        .GetRequiredService<IOptions<DatabaseOptions>>().Value;

    if (databaseOptions.RunMigrationsOnStartup)
    {
        app.Services.GetRequiredService<DatabaseMigrator>().Migrate();
    }
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapSettingsEndpoints();
app.MapAdminUserEndpoints();

app.Run();

/// <summary>
/// Exposed so Unify.Api.Tests can boot the real application through WebApplicationFactory.
/// </summary>
public partial class Program;
