using Unify.Domain.Authentication;

namespace Unify.Application.Abstractions.Persistence;

public interface ILoginAttemptRepository
{
    Task AddAsync(LoginAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consecutive failures for this account since the last success, within the window. Counting
    /// since the last success is what makes the threshold "consecutive" rather than cumulative.
    /// </summary>
    Task<int> CountRecentFailuresForUserAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Failures for an address that may not correspond to an existing account.</summary>
    Task<int> CountRecentFailuresForEmailAsync(
        string email,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Failures from one origin across every account (LOG-005).</summary>
    Task<int> CountRecentFailuresForIpAsync(
        string ipAddress,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Timestamp of the most recent failure, used to work out when a lockout expires.</summary>
    Task<DateTimeOffset?> GetLastFailureAtForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
