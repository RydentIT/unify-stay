using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Security;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// <paramref name="MustChangePassword"/> tells the client the returned token is a
/// <see cref="TokenScope.PasswordChangeOnly"/> credential and the only next step is the
/// change-password screen.
/// </summary>
public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    bool MustChangePassword);

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<LoginResult>>;
