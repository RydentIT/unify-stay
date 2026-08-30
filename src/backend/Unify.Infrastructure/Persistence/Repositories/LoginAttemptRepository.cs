using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Authentication;

namespace Unify.Infrastructure.Persistence.Repositories;

/// <summary>
/// Backs the lockout counters. The per-user count deliberately measures failures SINCE THE LAST
/// SUCCESS, which is what makes the threshold "consecutive": a successful sign-in resets the
/// counter without needing a separate cleanup job.
/// </summary>
internal sealed class LoginAttemptRepository : ILoginAttemptRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LoginAttemptRepository(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task AddAsync(LoginAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO login_attempts (id, user_id, email, ip_address, success, attempted_at)
            VALUES (@Id, @UserId, @Email, @IpAddress, @Success, @AttemptedAt);
            """,
            new
            {
                attempt.Id,
                attempt.UserId,
                attempt.Email,
                attempt.IpAddress,
                attempt.Success,
                attempt.AttemptedAt,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> CountRecentFailuresForUserAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int
            FROM   login_attempts
            WHERE  user_id = @UserId
              AND  success = false
              AND  attempted_at >= @Since
              AND  attempted_at > COALESCE(
                     (SELECT max(attempted_at)
                      FROM   login_attempts
                      WHERE  user_id = @UserId AND success = true),
                     'epoch'::timestamptz);
            """,
            new { UserId = userId, Since = since },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> CountRecentFailuresForEmailAsync(
        string email,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int
            FROM   login_attempts
            WHERE  email = @Email AND success = false AND attempted_at >= @Since;
            """,
            new { Email = email, Since = since },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> CountRecentFailuresForIpAsync(
        string ipAddress,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int
            FROM   login_attempts
            WHERE  ip_address = @IpAddress AND success = false AND attempted_at >= @Since;
            """,
            new { IpAddress = ipAddress, Since = since },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetLastFailureAtForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // Read as DateTime, not DateTimeOffset. Npgsql surfaces timestamptz as a UTC DateTime,
        // and Dapper's scalar path converts via System.Convert, which has no DateTime ->
        // DateTimeOffset conversion and throws. Row-mapped queries are unaffected because they
        // take a different conversion path.
        DateTime? lastFailure = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            """
            SELECT max(attempted_at)
            FROM   login_attempts
            WHERE  user_id = @UserId AND success = false;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return lastFailure is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(lastFailure.Value, DateTimeKind.Utc));
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);
}
