using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand<Result>;
