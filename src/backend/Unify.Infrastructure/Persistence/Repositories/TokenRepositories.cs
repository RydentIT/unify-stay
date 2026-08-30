using System.Data.Common;
using Dapper;
using Unify.Application.Abstractions.Persistence;
using Unify.Domain.Authentication;
using Unify.Domain.Users;
using Unify.Infrastructure.Persistence.Rows;

namespace Unify.Infrastructure.Persistence.Repositories;

/// <summary>
/// The three single-use token tables share a shape, so they share a base. Only the table name
/// and the mapping differ.
///
/// "Invalidate" is implemented as stamping used_at rather than deleting: keeping the spent row
/// is what lets the verification flow tell "already used" apart from "never existed" (EVR-010).
/// </summary>
internal abstract class SingleUseTokenRepositoryBase
{
    protected SingleUseTokenRepositoryBase(IDbConnectionFactory connectionFactory, string tableName)
    {
        ConnectionFactory = connectionFactory;
        TableName = tableName;
    }

    protected IDbConnectionFactory ConnectionFactory { get; }

    protected string TableName { get; }

    protected string SelectColumns => $"""
        SELECT id         AS Id,
               user_id    AS UserId,
               token_hash AS TokenHash,
               expires_at AS ExpiresAt,
               used_at    AS UsedAt,
               created_at AS CreatedAt
        FROM   {TableName}
        """;

    protected Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        ConnectionFactory.OpenConnectionAsync(cancellationToken);

    protected async Task InsertAsync(SingleUseToken token, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO {TableName} (id, user_id, token_hash, expires_at, used_at, created_at)
            VALUES (@Id, @UserId, @TokenHash, @ExpiresAt, @UsedAt, @CreatedAt);
            """,
            new { token.Id, token.UserId, token.TokenHash, token.ExpiresAt, token.UsedAt, token.CreatedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    protected async Task<TokenRow?> SelectByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.QuerySingleOrDefaultAsync<TokenRow>(new CommandDefinition(
            SelectColumns + " WHERE token_hash = @TokenHash;",
            new { TokenHash = tokenHash },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task MarkUsedAsync(Guid tokenId, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {TableName} SET used_at = COALESCE(used_at, @UsedAt) WHERE id = @Id;",
            new { Id = tokenId, UsedAt = usedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InvalidateAllForUserAsync(
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {TableName} SET used_at = @At WHERE user_id = @UserId AND used_at IS NULL;",
            new { UserId = userId, At = at },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

internal sealed class PasswordResetTokenRepository : SingleUseTokenRepositoryBase, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory, "password_reset_tokens")
    {
    }

    public Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
        InsertAsync(token, cancellationToken);

    public async Task<PasswordResetToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        TokenRow? row = await SelectByHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        return row is null
            ? null
            : new PasswordResetToken(row.Id, row.UserId, row.TokenHash, row.ExpiresAt, row.CreatedAt, row.UsedAt);
    }
}

internal sealed class EmailVerificationTokenRepository
    : SingleUseTokenRepositoryBase, IEmailVerificationTokenRepository
{
    public EmailVerificationTokenRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory, "email_verification_tokens")
    {
    }

    public Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken = default) =>
        InsertAsync(token, cancellationToken);

    public async Task<EmailVerificationToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        TokenRow? row = await SelectByHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        return row is null
            ? null
            : new EmailVerificationToken(row.Id, row.UserId, row.TokenHash, row.ExpiresAt, row.CreatedAt, row.UsedAt);
    }

    public async Task<int> CountIssuedSinceAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int
            FROM   email_verification_tokens
            WHERE  user_id = @UserId AND created_at >= @Since;
            """,
            new { UserId = userId, Since = since },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

internal sealed class PendingEmailChangeRepository : IPendingEmailChangeRepository
{
    private const string SelectColumns = """
        SELECT id         AS Id,
               user_id    AS UserId,
               new_email  AS NewEmail,
               token_hash AS TokenHash,
               expires_at AS ExpiresAt,
               used_at    AS UsedAt,
               created_at AS CreatedAt
        FROM   pending_email_changes
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public PendingEmailChangeRepository(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task AddAsync(PendingEmailChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO pending_email_changes (id, user_id, new_email, token_hash, expires_at, used_at, created_at)
            VALUES (@Id, @UserId, @NewEmail, @TokenHash, @ExpiresAt, @UsedAt, @CreatedAt);
            """,
            new
            {
                change.Id,
                change.UserId,
                NewEmail = change.NewEmail.Value,
                change.TokenHash,
                change.ExpiresAt,
                change.UsedAt,
                change.CreatedAt,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<PendingEmailChange?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        PendingEmailChangeRow? row = await connection.QuerySingleOrDefaultAsync<PendingEmailChangeRow>(
            new CommandDefinition(
                SelectColumns + " WHERE token_hash = @TokenHash;",
                new { TokenHash = tokenHash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task<PendingEmailChange?> GetActiveForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        PendingEmailChangeRow? row = await connection.QuerySingleOrDefaultAsync<PendingEmailChangeRow>(
            new CommandDefinition(
                SelectColumns + """
                 WHERE user_id = @UserId AND used_at IS NULL AND expires_at > @Now
                 ORDER BY created_at DESC
                 LIMIT 1;
                """,
                new { UserId = userId, Now = now },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : Map(row);
    }

    public async Task MarkUsedAsync(Guid changeId, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE pending_email_changes SET used_at = COALESCE(used_at, @UsedAt) WHERE id = @Id;",
            new { Id = changeId, UsedAt = usedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InvalidateAllForUserAsync(
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE pending_email_changes SET used_at = @At WHERE user_id = @UserId AND used_at IS NULL;",
            new { UserId = userId, At = at },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> IsEmailTakenAsync(Email email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        await using DbConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // Only unspent, unexpired requests reserve an address; a lapsed one must not block it.
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1 FROM pending_email_changes
                WHERE new_email = @Email AND used_at IS NULL AND expires_at > now());
            """,
            new { Email = email.Value },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        _connectionFactory.OpenConnectionAsync(cancellationToken);

    private static PendingEmailChange Map(PendingEmailChangeRow row) => new(
        row.Id,
        row.UserId,
        Email.Create(row.NewEmail),
        row.TokenHash,
        row.ExpiresAt,
        row.CreatedAt,
        row.UsedAt);
}
