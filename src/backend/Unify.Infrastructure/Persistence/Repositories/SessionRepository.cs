using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Authentication;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Rows;

namespace Unify.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository : ISessionRepository
{
    private const string SelectColumns = """
        SELECT id                 AS Id,
               user_id            AS UserId,
               refresh_token_hash AS RefreshTokenHash,
               role_claim         AS RoleClaim,
               expires_at         AS ExpiresAt,
               revoked_at         AS RevokedAt,
               created_at         AS CreatedAt
        FROM   sessions
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public SessionRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sessions (id, user_id, refresh_token_hash, role_claim, expires_at, revoked_at, created_at)
            VALUES (@Id, @UserId, @RefreshTokenHash, @RoleClaim, @ExpiresAt, @RevokedAt, @CreatedAt);
            """,
            new
            {
                session.Id,
                session.UserId,
                session.RefreshTokenHash,
                RoleClaim = session.RoleClaim?.ToString(),
                session.ExpiresAt,
                session.RevokedAt,
                session.CreatedAt,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Session?> GetByRefreshTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        SessionRow? row = await connection.QuerySingleOrDefaultAsync<SessionRow>(
            new CommandDefinition(
                SelectColumns + " WHERE refresh_token_hash = @TokenHash;",
                new { TokenHash = tokenHash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // COALESCE keeps the original revocation time: revoking twice must not extend the life
        // of anything, and the first revocation is the one that matters for an audit.
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sessions SET revoked_at = COALESCE(revoked_at, @RevokedAt) WHERE id = @Id;",
            new { Id = sessionId, RevokedAt = revokedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sessions SET revoked_at = @RevokedAt WHERE user_id = @UserId AND revoked_at IS NULL;",
            new { UserId = userId, RevokedAt = revokedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserExceptAsync(
        Guid userId,
        Guid exceptSessionId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sessions
            SET    revoked_at = @RevokedAt
            WHERE  user_id = @UserId AND id <> @ExceptId AND revoked_at IS NULL;
            """,
            new { UserId = userId, ExceptId = exceptSessionId, RevokedAt = revokedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> CountActiveForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int
            FROM   sessions
            WHERE  user_id = @UserId AND revoked_at IS NULL AND expires_at > @Now;
            """,
            new { UserId = userId, Now = now },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);

    private static Session Map(SessionRow row) => new(
        row.Id,
        row.UserId,
        row.RefreshTokenHash,
        Enum.TryParse(row.RoleClaim, out RoleName role) ? role : null,
        row.ExpiresAt,
        row.CreatedAt,
        row.RevokedAt);
}
