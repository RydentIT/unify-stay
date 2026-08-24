using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Notifications;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.RequestPasswordReset;

/// <summary>
/// SCAFFOLD STUB - no reset logic yet, by design.
/// When implemented this must return the same result whether or not the address exists, so
/// the endpoint cannot be used to enumerate accounts.
/// </summary>
internal sealed class RequestPasswordResetCommandHandler
    : ICommandHandler<RequestPasswordResetCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogger _auditLogger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RequestPasswordResetCommandHandler(
        IUserRepository users,
        IEmailSender emailSender,
        IAuditLogger auditLogger,
        IDateTimeProvider dateTimeProvider)
    {
        _users = users;
        _emailSender = emailSender;
        _auditLogger = auditLogger;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<Result> HandleAsync(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;

        return Task.FromResult(Result.Failure(Error.NotImplemented(
            "auth.password_reset.not_implemented",
            "Password reset is not implemented yet.")));
    }
}
