using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// SCAFFOLD STUB - no authentication logic yet, by design.
/// Replace the body (not the signature) when the Login module lands.
/// </summary>
internal sealed class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResult>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogger _auditLogger;

    public LoginCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogger auditLogger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
    }

    public Task<Result<LoginResult>> HandleAsync(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;

        return Task.FromResult(Result.Failure<LoginResult>(Error.NotImplemented(
            "auth.login.not_implemented",
            "Login is not implemented yet.")));
    }
}
