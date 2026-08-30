using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Features.Authentication.EmailVerification;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Features.Bootstrap;

public enum AdminBootstrapOutcome
{
    /// <summary>An Admin already exists. Nothing was created.</summary>
    AlreadyExists,

    /// <summary>ADMIN_BOOTSTRAP_EMAIL / ADMIN_BOOTSTRAP_PASSWORD were not both set.</summary>
    MissingConfiguration,

    Created,
}

/// <summary>
/// Creates the first Admin account. Invoked explicitly via `dotnet run -- bootstrap-admin` -
/// never run automatically on startup, so an operator has to mean to do this.
///
/// The created account is unverified (email_verified=false), the same representation the rest
/// of the system already uses for "not yet usable" - there is no separate "pending" status in
/// this domain, and inventing one just for bootstrap would be a second way to say the same
/// thing. LOG-004 blocks sign-in for any unverified account regardless of role, so the admin
/// genuinely cannot log in until the verification email is confirmed.
/// </summary>
public sealed class AdminBootstrapper
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDispatcher _dispatcher;
    private readonly IDateTimeProvider _clock;
    private readonly AdminBootstrapOptions _options;
    private readonly ILogger<AdminBootstrapper> _logger;

    public AdminBootstrapper(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IDispatcher dispatcher,
        IDateTimeProvider clock,
        IOptions<AdminBootstrapOptions> options,
        ILogger<AdminBootstrapper> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _users = users;
        _passwordHasher = passwordHasher;
        _dispatcher = dispatcher;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdminBootstrapOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent: safe to run accidentally, including on every deploy if someone wires it
        // into a pipeline by mistake. Never a second Admin from a second invocation.
        if (await _users.ExistsAnyWithRoleAsync(RoleName.Admin, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "An Admin account already exists. Bootstrap is a no-op - nothing was created.");

            return AdminBootstrapOutcome.AlreadyExists;
        }

        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogError(
                "Cannot bootstrap an Admin: set both ADMIN_BOOTSTRAP_EMAIL and ADMIN_BOOTSTRAP_PASSWORD " +
                "and run this command again.");

            return AdminBootstrapOutcome.MissingConfiguration;
        }

        var email = Email.Create(_options.Email);
        DateTimeOffset now = _clock.UtcNow;

        // Hashed immediately; the raw value from _options.Password is never passed to anything
        // else, including any log call below - only the account's email is ever logged.
        string passwordHash = _passwordHasher.Hash(_options.Password);

        var admin = new User(
            Guid.CreateVersion7(),
            firstName: "Admin",
            lastName: "Account",
            email,
            passwordHash,
            emailVerified: false,
            mustChangePassword: false,
            status: UserStatus.Active,
            createdAt: now,
            updatedAt: now);

        admin.AddRole(RoleName.Admin);
        admin.AddAuthProvider(AuthProvider.Local(Guid.CreateVersion7(), admin.Id, now));

        await _users.AddAsync(admin, cancellationToken).ConfigureAwait(false);

        // Reuses the existing verification flow end to end: same token table, same expiry, same
        // VerifyEmailCommand the register endpoint relies on. No parallel implementation exists.
        await _dispatcher
            .SendAsync(new SendVerificationEmailCommand(admin.Id), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Created the first Admin account ({Email}). A verification email was sent - " +
            "the account cannot sign in until it is confirmed.",
            admin.Email.Value);

        return AdminBootstrapOutcome.Created;
    }
}
