using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// Ends a session server-side. The refresh token identifies which one; when it is absent every
/// session for the caller is revoked, which is the safe reading of "log me out".
/// </summary>
public sealed record LogoutCommand(string? RefreshToken) : ICommand<Result>;
