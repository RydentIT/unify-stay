using Unify.Domain.Auditing;
using Unify.Domain.Authentication;
using Unify.Domain.Settings;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Repositories;
using Unify.Infrastructure.Tests.Infrastructure;

namespace Unify.Infrastructure.Tests.Persistence;

/// <summary>
/// The session, lockout, token and upgrade-request SQL. The lockout counters in particular are
/// worth exercising for real: they lean on a correlated subquery that no unit test can validate.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public AuthRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task<User> SeedUserAsync(string email = "auth@example.com")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var user = new User(
            Guid.CreateVersion7(),
            "Auth",
            "User",
            Email.Create(email),
            "hashed",
            emailVerified: true,
            mustChangePassword: false,
            UserStatus.Active,
            now,
            now);

        user.AddRole(RoleName.Student);

        await new UserRepository(_postgres.CreateConnectionFactory()).AddAsync(user);
        return user;
    }

    // ---------------------------------------------------------------- Sessions

    [RequiresPostgresFact]
    public async Task Revokes_a_single_session_and_leaves_the_others_alone()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var sessions = new SessionRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var first = new Session(Guid.CreateVersion7(), user.Id, "hash-1", RoleName.Student, now.AddDays(7), now);
        var second = new Session(Guid.CreateVersion7(), user.Id, "hash-2", RoleName.Student, now.AddDays(7), now);

        await sessions.AddAsync(first);
        await sessions.AddAsync(second);

        await sessions.RevokeAsync(first.Id, now);

        Assert.Equal(1, await sessions.CountActiveForUserAsync(user.Id, now));

        Session? reloaded = await sessions.GetByRefreshTokenHashAsync("hash-1");
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsRevoked);
    }

    /// <summary>BR-PRF-003: change-password spares the caller's own session.</summary>
    [RequiresPostgresFact]
    public async Task Revokes_all_sessions_except_the_named_one()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var sessions = new SessionRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var keep = new Session(Guid.CreateVersion7(), user.Id, "keep", RoleName.Student, now.AddDays(7), now);
        var drop1 = new Session(Guid.CreateVersion7(), user.Id, "drop-1", RoleName.Student, now.AddDays(7), now);
        var drop2 = new Session(Guid.CreateVersion7(), user.Id, "drop-2", RoleName.Student, now.AddDays(7), now);

        await sessions.AddAsync(keep);
        await sessions.AddAsync(drop1);
        await sessions.AddAsync(drop2);

        await sessions.RevokeAllForUserExceptAsync(user.Id, keep.Id, now);

        Assert.Equal(1, await sessions.CountActiveForUserAsync(user.Id, now));

        Session? survivor = await sessions.GetByRefreshTokenHashAsync("keep");
        Assert.NotNull(survivor);
        Assert.False(survivor.IsRevoked);
    }

    [RequiresPostgresFact]
    public async Task Revokes_every_session_for_a_user()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var sessions = new SessionRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await sessions.AddAsync(new Session(Guid.CreateVersion7(), user.Id, "s1", RoleName.Student, now.AddDays(7), now));
        await sessions.AddAsync(new Session(Guid.CreateVersion7(), user.Id, "s2", RoleName.Student, now.AddDays(7), now));

        await sessions.RevokeAllForUserAsync(user.Id, now);

        Assert.Equal(0, await sessions.CountActiveForUserAsync(user.Id, now));
    }

    // ---------------------------------------------------------------- Lockout counters

    /// <summary>
    /// LOG-006 hinges on this being CONSECUTIVE: a success in between must reset the count.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Failure_count_only_covers_attempts_since_the_last_success()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var attempts = new LoginAttemptRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await attempts.AddAsync(LoginAttempt.Failure(user.Id, user.Email.Value, "10.0.0.1", now.AddMinutes(-10)));
        await attempts.AddAsync(LoginAttempt.Failure(user.Id, user.Email.Value, "10.0.0.1", now.AddMinutes(-9)));
        await attempts.AddAsync(LoginAttempt.Successful(user.Id, user.Email.Value, "10.0.0.1", now.AddMinutes(-8)));
        await attempts.AddAsync(LoginAttempt.Failure(user.Id, user.Email.Value, "10.0.0.1", now.AddMinutes(-1)));

        int count = await attempts.CountRecentFailuresForUserAsync(user.Id, now.AddMinutes(-30));

        // Only the single failure after the success counts.
        Assert.Equal(1, count);
    }

    [RequiresPostgresFact]
    public async Task Counts_failures_per_ip_across_accounts()
    {
        await _postgres.ResetAsync();
        User first = await SeedUserAsync("ip-one@example.com");
        User second = await SeedUserAsync("ip-two@example.com");

        var attempts = new LoginAttemptRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await attempts.AddAsync(LoginAttempt.Failure(first.Id, first.Email.Value, "198.51.100.9", now.AddMinutes(-3)));
        await attempts.AddAsync(LoginAttempt.Failure(second.Id, second.Email.Value, "198.51.100.9", now.AddMinutes(-2)));
        await attempts.AddAsync(LoginAttempt.Failure(null, "ghost@example.com", "198.51.100.9", now.AddMinutes(-1)));

        int count = await attempts.CountRecentFailuresForIpAsync("198.51.100.9", now.AddMinutes(-30));

        Assert.Equal(3, count);
    }

    /// <summary>Attempts against an address with no account still have to be recorded.</summary>
    [RequiresPostgresFact]
    public async Task Records_a_failure_for_an_unknown_address()
    {
        await _postgres.ResetAsync();

        var attempts = new LoginAttemptRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await attempts.AddAsync(LoginAttempt.Failure(null, "ghost@example.com", "203.0.113.1", now));

        Assert.Equal(1, await attempts.CountRecentFailuresForEmailAsync("ghost@example.com", now.AddMinutes(-5)));
    }

    [RequiresPostgresFact]
    public async Task Reads_back_the_last_failure_timestamp()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var attempts = new LoginAttemptRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await attempts.AddAsync(LoginAttempt.Failure(user.Id, user.Email.Value, "10.0.0.2", now.AddMinutes(-5)));
        await attempts.AddAsync(LoginAttempt.Failure(user.Id, user.Email.Value, "10.0.0.2", now.AddMinutes(-2)));

        DateTimeOffset? last = await attempts.GetLastFailureAtForUserAsync(user.Id);

        Assert.NotNull(last);
        // Within a second of the most recent failure, allowing for round-trip precision.
        Assert.True(Math.Abs((last.Value - now.AddMinutes(-2)).TotalSeconds) < 1);
    }

    // ---------------------------------------------------------------- Tokens

    [RequiresPostgresFact]
    public async Task Password_reset_tokens_round_trip_and_invalidate()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var tokens = new PasswordResetTokenRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var token = new PasswordResetToken(
            Guid.CreateVersion7(), user.Id, "reset-hash", now.AddHours(1), now);

        await tokens.AddAsync(token);

        PasswordResetToken? loaded = await tokens.GetByHashAsync("reset-hash");
        Assert.NotNull(loaded);
        Assert.True(loaded.IsRedeemable(now));

        await tokens.InvalidateAllForUserAsync(user.Id, now);

        PasswordResetToken? afterInvalidate = await tokens.GetByHashAsync("reset-hash");
        Assert.NotNull(afterInvalidate);
        Assert.True(afterInvalidate.IsUsed);
        Assert.False(afterInvalidate.IsRedeemable(now));
    }

    /// <summary>BR-EVR-002 in SQL: issuing a new token retires the old one.</summary>
    [RequiresPostgresFact]
    public async Task Verification_token_invalidation_only_affects_outstanding_tokens()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var tokens = new EmailVerificationTokenRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // A token that was already spent before the bulk invalidation runs.
        DateTimeOffset originalUsedAt = now.AddMinutes(-5);
        await tokens.AddAsync(new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "already-used", now.AddHours(1), now, originalUsedAt));

        var outstanding = new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "outstanding", now.AddHours(1), now);
        await tokens.AddAsync(outstanding);

        await tokens.InvalidateAllForUserAsync(user.Id, now);

        EmailVerificationToken? previouslyUsed = await tokens.GetByHashAsync("already-used");
        EmailVerificationToken? nowUsed = await tokens.GetByHashAsync("outstanding");

        Assert.NotNull(previouslyUsed);
        Assert.NotNull(nowUsed);

        // The original used_at is preserved, not overwritten by the bulk invalidation.
        Assert.Equal(originalUsedAt.ToUnixTimeSeconds(), previouslyUsed.UsedAt!.Value.ToUnixTimeSeconds());
        Assert.True(nowUsed.IsUsed);
    }

    [RequiresPostgresFact]
    public async Task Counts_verification_tokens_issued_within_a_window()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var tokens = new EmailVerificationTokenRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await tokens.AddAsync(new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "old", now.AddHours(1), now.AddHours(-5)));
        await tokens.AddAsync(new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "recent-1", now.AddHours(1), now.AddMinutes(-10)));
        await tokens.AddAsync(new EmailVerificationToken(
            Guid.CreateVersion7(), user.Id, "recent-2", now.AddHours(1), now.AddMinutes(-5)));

        int recent = await tokens.CountIssuedSinceAsync(user.Id, now.AddHours(-1));

        Assert.Equal(2, recent);
    }

    [RequiresPostgresFact]
    public async Task Pending_email_change_reserves_the_target_address()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var pending = new PendingEmailChangeRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await pending.AddAsync(new PendingEmailChange(
            Guid.CreateVersion7(), user.Id, Email.Create("moved@example.com"),
            "change-hash", now.AddHours(24), now));

        Assert.True(await pending.IsEmailTakenAsync(Email.Create("moved@example.com")));
        Assert.False(await pending.IsEmailTakenAsync(Email.Create("other@example.com")));

        PendingEmailChange? active = await pending.GetActiveForUserAsync(user.Id, now);
        Assert.NotNull(active);
        Assert.Equal("moved@example.com", active.NewEmail.Value);
    }

    /// <summary>An expired request must stop reserving the address.</summary>
    [RequiresPostgresFact]
    public async Task An_expired_pending_change_no_longer_reserves_the_address()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var pending = new PendingEmailChangeRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await pending.AddAsync(new PendingEmailChange(
            Guid.CreateVersion7(), user.Id, Email.Create("lapsed@example.com"),
            "lapsed-hash", now.AddMinutes(-1), now.AddHours(-25)));

        Assert.False(await pending.IsEmailTakenAsync(Email.Create("lapsed@example.com")));
    }

    // ---------------------------------------------------------------- Upgrade requests

    private static RoleUpgradeRequest NewUpgradeRequest(Guid userId, DateTimeOffset submittedAt) =>
        RoleUpgradeRequest.Submit(
            Guid.CreateVersion7(),
            userId,
            "199912345678",
            "123 Galle Road, Colombo",
            "+94711234567",
            "Two-bedroom apartment.",
            submittedAt);

    /// <summary>BR-SET-001 is enforced by a partial unique index, not just by the handler.</summary>
    [RequiresPostgresFact]
    public async Task The_database_refuses_a_second_active_upgrade_request()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var requests = new RoleUpgradeRequestRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await requests.AddAsync(NewUpgradeRequest(user.Id, now));

        await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => requests.AddAsync(NewUpgradeRequest(user.Id, now)));
    }

    /// <summary>SET-008: after a rejection the slot frees up.</summary>
    [RequiresPostgresFact]
    public async Task A_rejected_request_allows_a_resubmission()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var requests = new RoleUpgradeRequestRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RoleUpgradeRequest first = NewUpgradeRequest(user.Id, now.AddDays(-2));
        await requests.AddAsync(first);

        first.Reject(user.Id, "NIC does not match the name on file.", now.AddDays(-1));
        await requests.UpdateAsync(first);

        Assert.Null(await requests.GetActiveForUserAsync(user.Id));

        RoleUpgradeRequest? latest = await requests.GetLatestForUserAsync(user.Id);
        Assert.NotNull(latest);
        Assert.Equal(UpgradeRequestStatus.Rejected, latest.Status);
        Assert.Equal("NIC does not match the name on file.", latest.RejectionReason);

        // The slot is free again.
        RoleUpgradeRequest second = NewUpgradeRequest(user.Id, now);
        await requests.AddAsync(second);

        Assert.NotNull(await requests.GetActiveForUserAsync(user.Id));
    }

    /// <summary>Confirms the structured fields round-trip through the hand-written SQL.</summary>
    [RequiresPostgresFact]
    public async Task Round_trips_the_structured_upgrade_fields()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync();

        var requests = new RoleUpgradeRequestRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RoleUpgradeRequest request = RoleUpgradeRequest.Submit(
            Guid.CreateVersion7(),
            user.Id,
            "912345678V",
            "45 Lake Drive, Kandy",
            "+94770000000",
            "Three-bedroom house with parking.",
            now);

        await requests.AddAsync(request);

        RoleUpgradeRequest? loaded = await requests.GetByIdAsync(request.Id);

        Assert.NotNull(loaded);
        Assert.Equal("912345678V", loaded.NicNumber);
        Assert.Equal("45 Lake Drive, Kandy", loaded.Address);
        Assert.Equal("+94770000000", loaded.PhoneNumber2);
        Assert.Equal("Three-bedroom house with parking.", loaded.PropertyInfo);
    }

    // ---------------------------------------------------------------- Audit

    /// <summary>BR-SET-004: audit rows carry the email as a value and outlive the account.</summary>
    [RequiresPostgresFact]
    public async Task Audit_entries_survive_the_account_being_deleted()
    {
        await _postgres.ResetAsync();
        User user = await SeedUserAsync("audited@example.com");

        var audit = new AuditLogRepository(_postgres.CreateConnectionFactory());
        var users = new UserRepository(_postgres.CreateConnectionFactory());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            Guid.CreateVersion7(), "settings.account.deleted", now, user.Id,
            "audited@example.com", "local", "203.0.113.7", """{"reason":"user requested"}"""));

        user.MarkDeleted(now);
        await users.UpdateAsync(user);

        IReadOnlyList<AuditLogEntry> entries = await audit.ListForUserAsync(user.Id, 10);

        AuditLogEntry entry = Assert.Single(entries);
        Assert.Equal("settings.account.deleted", entry.ActionType);
        Assert.Equal("audited@example.com", entry.Email);
        Assert.Contains("user requested", entry.FieldsChanged, StringComparison.Ordinal);
    }
}
