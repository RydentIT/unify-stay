using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.Register;

public sealed record RegisterResult(Guid UserId, string Email, bool EmailVerificationRequired);

/// <summary>
/// Email + password registration (REG-003, REG-006, REG-007).
/// <paramref name="AcceptedTerms"/> must be true before the account is created (REG-006).
/// <paramref name="ContactNumber"/> is required at registration for this path, unlike Google
/// registration where it cannot be collected from the ID token and is deferred to a mandatory
/// post-registration step instead.
/// </summary>
public sealed record RegisterWithEmailCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ContactNumber,
    bool AcceptedTerms) : ICommand<Result<RegisterResult>>;
