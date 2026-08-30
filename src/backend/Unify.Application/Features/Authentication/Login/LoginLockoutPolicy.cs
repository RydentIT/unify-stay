using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Options;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>Result of the pre-authentication lockout check.</summary>
internal sealed record LockoutState(bool IsLockedOut, DateTimeOffset? LockedUntil);

/// <summary>
/// LOG-005 / LOG-006 / BR-LOG-002.
///
/// Two independent counters, because they defend against different things:
///   * per account - stops someone grinding one victim's password.
///   * per IP      - stops one origin spraying many accounts, which the per-account counter
///                   would never notice since each account only sees one or two failures.
///
/// Both thresholds and the lockout duration come from configuration; nothing here is hardcoded.
/// </summary>
internal sealed class LoginLockoutPolicy
{
    private readonly ILoginAttemptRepository _attempts;
    private readonly IDateTimeProvider _clock;
    private readonly AuthOptions _auth;

    public LoginLockoutPolicy(
        ILoginAttemptRepository attempts,
        IDateTimeProvider clock,
        IOptions<AuthOptions> auth)
    {
        ArgumentNullException.ThrowIfNull(auth);

        _attempts = attempts;
        _clock = clock;
        _auth = auth.Value;
    }

    public async Task<LockoutState> EvaluateAsync(
        User? user,
        string email,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        DateTimeOffset windowStart = now - _auth.FailedAttemptWindow;

        if (user is not null)
        {
            int failures = await _attempts
                .CountRecentFailuresForUserAsync(user.Id, windowStart, cancellationToken)
                .ConfigureAwait(false);

            if (failures >= _auth.MaxFailedLoginAttempts)
            {
                DateTimeOffset? lastFailure = await _attempts
                    .GetLastFailureAtForUserAsync(user.Id, cancellationToken)
                    .ConfigureAwait(false);

                DateTimeOffset lockedUntil = (lastFailure ?? now) + _auth.Lockout;

                // Once the lockout window has elapsed the counter is effectively reset, because
                // the older failures fall outside FailedAttemptWindow on the next evaluation.
                if (lockedUntil > now)
                {
                    return new LockoutState(true, lockedUntil);
                }
            }
        }
        else
        {
            // No account for this address. Still counted, so probing unknown addresses is not a
            // free way to stay under the per-account threshold.
            int failuresForEmail = await _attempts
                .CountRecentFailuresForEmailAsync(email, windowStart, cancellationToken)
                .ConfigureAwait(false);

            if (failuresForEmail >= _auth.MaxFailedLoginAttempts)
            {
                return new LockoutState(true, now + _auth.Lockout);
            }
        }

        if (!string.IsNullOrEmpty(ipAddress))
        {
            int ipFailures = await _attempts
                .CountRecentFailuresForIpAsync(ipAddress, windowStart, cancellationToken)
                .ConfigureAwait(false);

            if (ipFailures >= _auth.MaxFailedAttemptsPerIpAddress)
            {
                return new LockoutState(true, now + _auth.Lockout);
            }
        }

        return new LockoutState(false, null);
    }

    /// <summary>True when this failure is the one that trips the threshold, so the user can be told.</summary>
    public async Task<bool> RecordFailureAsync(
        Guid? userId,
        string? email,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;

        await _attempts
            .AddAsync(LoginAttempt.Failure(userId, email, ipAddress, now), cancellationToken)
            .ConfigureAwait(false);

        if (userId is null)
        {
            return false;
        }

        int failures = await _attempts
            .CountRecentFailuresForUserAsync(userId.Value, now - _auth.FailedAttemptWindow, cancellationToken)
            .ConfigureAwait(false);

        return failures >= _auth.MaxFailedLoginAttempts;
    }

    public Task RecordSuccessAsync(
        Guid userId,
        string? email,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        _attempts.AddAsync(
            LoginAttempt.Successful(userId, email, ipAddress, _clock.UtcNow),
            cancellationToken);

    public DateTimeOffset LockedUntilFrom(DateTimeOffset now) => now + _auth.Lockout;
}
