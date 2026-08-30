using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.EmailVerification;

/// <summary>
/// Issues a fresh verification token and emails the link (EVR-001). Dispatched by registration
/// and by the resend endpoint, so the "invalidate the old token first" rule lives in one place.
/// </summary>
public sealed record SendVerificationEmailCommand(Guid UserId) : ICommand<Result>;
