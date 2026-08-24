using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.RegisterUser;

/// <summary>
/// SCAFFOLD STUB - no registration logic yet, by design.
/// The dependencies below are the ones the real handler will use; they are taken now so the
/// DI graph, the validation pipeline and the endpoint wiring are all exercised for real.
/// Replace the body (not the signature) when the Register module lands.
/// </summary>
internal sealed class RegisterUserCommandHandler
    : ICommandHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterUserCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IAuditLogger auditLogger,
        IDateTimeProvider dateTimeProvider)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<Result<RegisterUserResult>> HandleAsync(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;

        return Task.FromResult(Result.Failure<RegisterUserResult>(Error.NotImplemented(
            "auth.register.not_implemented",
            "User registration is not implemented yet.")));
    }
}
