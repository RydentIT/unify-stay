using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.RegisterUser;

public sealed record RegisterUserResult(Guid UserId, string Email);

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    bool AcceptedTerms) : ICommand<Result<RegisterUserResult>>;
