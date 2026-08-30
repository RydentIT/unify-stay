using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.EmailVerification;

public sealed record VerifyEmailCommand(string Token) : ICommand<Result>;

/// <summary>Resend is a separate command because it is rate limited independently (EVR-006).</summary>
public sealed record ResendVerificationEmailCommand(string Email) : ICommand<Result>;
