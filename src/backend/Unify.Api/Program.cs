using Microsoft.Extensions.Options;
using Unify.Api.Endpoints;
using Unify.Api.Extensions;
using Unify.Api.Identity;
using Unify.Api.Middleware;
using Unify.Application;
using Unify.Application.Abstractions.Identity;
using Unify.Infrastructure;
using Unify.Infrastructure.Migrations;
using Unify.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration precedence: appsettings.json (placeholders only) < appsettings.{Env}.json <
// user secrets in Development < environment variables. Real secrets only ever arrive from
// the last two, which is why the committed files are blank.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddTransient<GlobalExceptionHandlingMiddleware>();

builder.Services.AddUnifyAuthentication(builder.Configuration);
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

    DatabaseOptions databaseOptions = app.Services
        .GetRequiredService<IOptions<DatabaseOptions>>().Value;

    if (databaseOptions.RunMigrationsOnStartup)
    {
        app.Services.GetRequiredService<DatabaseMigrator>().Migrate();
    }
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();

app.Run();

/// <summary>
/// Exposed so Unify.Api.Tests can boot the real application through WebApplicationFactory.
/// </summary>
public partial class Program;
