using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.EmailVerification;

/// <summary>
/// EVR-010 is the point of this handler: expired, already-used and unrecognised are three
/// different answers, because they need three different next actions from the user (request a
/// new link / just sign in / check the link). Collapsing them into one message would be
/// friendlier to nobody.
/// </summary>
internal sealed class VerifyEmailCommandHandler : ICommandHandler<VerifyEmailCommand, Result>
{
    private readonly IEmailVerificationTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IAuditLogger _auditLogger;
    private readonly IDateTimeProvider _clock;

    public VerifyEmailCommandHandler(
        IEmailVerificationTokenRepository tokens,
        IUserRepository users,
        ISecureTokenGenerator tokenGenerator,
        IAuditLogger auditLogger,
        IDateTimeProvider clock)
    {
        _tokens = tokens;
        _users = users;
        _tokenGenerator = tokenGenerator;
        _auditLogger = auditLogger;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        // Look the token up by hash: the raw value was only ever in the email.
        string hash = _tokenGenerator.Hash(request.Token);

        EmailVerificationToken? token = await _tokens
            .GetByHashAsync(hash, cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            await LogFailureAsync(null, "unrecognised", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.InvalidVerificationToken);
        }

        DateTimeOffset now = _clock.UtcNow;

        if (token.IsUsed)
        {
            await LogFailureAsync(token.UserId, "already_used", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.UsedVerificationToken);
        }

        if (token.IsExpired(now))
        {
            await LogFailureAsync(token.UserId, "expired", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.ExpiredVerificationToken);
        }

        User? user = await _users.GetByIdAsync(token.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure(AuthErrors.UserNotFound);
        }

        // Domain guard: a deleted account must not be resurrected by an old link.
        if (user.IsDeleted)
        {
            await LogFailureAsync(user.Id, "account_deleted", cancellationToken).ConfigureAwait(false);
            return Result.Failure(AuthErrors.InvalidVerificationToken);
        }

        user.MarkEmailVerified(now);

        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _tokens.MarkUsedAsync(token.Id, now, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.EmailVerified,
                UserId = user.Id,
                Email = user.Email.Value,
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private Task LogFailureAsync(Guid? userId, string reason, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.EmailVerificationFailed,
                UserId = userId,
                FieldsChanged = new { reason },
            },
            cancellationToken);
}
