using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.EmailVerification;

/// <summary>
/// EVR-006. Two things worth noting:
///
///  * The response is the same whether or not the address exists or is already verified. This
///    endpoint is unauthenticated, so a truthful answer would confirm which addresses have
///    accounts - the same enumeration concern that shapes the forgot-password flow.
///  * The per-account throttle here complements the per-IP rate limiter at the API edge; the
///    limiter alone would not stop a distributed attempt to flood one person's inbox.
/// </summary>
internal sealed class ResendVerificationEmailCommandHandler
    : ICommandHandler<ResendVerificationEmailCommand, Result>
{
    /// <summary>Verification emails allowed per account within the window below.</summary>
    private const int MaxResendsPerWindow = 3;

    private static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);

    private readonly IUserRepository _users;
    private readonly IEmailVerificationTokenRepository _tokens;
    private readonly IDispatcher _dispatcher;
    private readonly IDateTimeProvider _clock;

    public ResendVerificationEmailCommandHandler(
        IUserRepository users,
        IEmailVerificationTokenRepository tokens,
        IDispatcher dispatcher,
        IDateTimeProvider clock)
    {
        _users = users;
        _tokens = tokens;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        ResendVerificationEmailCommand request,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        User? user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        // Unknown address, already verified, or deleted: all answer the same way. Nothing is
        // sent, and the caller learns nothing about which case applied.
        if (user is null || user.EmailVerified || user.IsDeleted)
        {
            return Result.Success();
        }

        int recentlyIssued = await _tokens
            .CountIssuedSinceAsync(user.Id, _clock.UtcNow - ResendWindow, cancellationToken)
            .ConfigureAwait(false);

        if (recentlyIssued >= MaxResendsPerWindow)
        {
            // Silently decline rather than reporting the throttle, for the same reason as above.
            return Result.Success();
        }

        await _dispatcher
            .SendAsync(new SendVerificationEmailCommand(user.Id), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
