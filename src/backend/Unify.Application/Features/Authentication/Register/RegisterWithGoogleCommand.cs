using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.Register;

/// <summary>
/// Google registration. The client sends only the ID token: the email, the name and the
/// verified flag are read from the token AFTER server-side signature validation, never from
/// anything the browser claims.
/// </summary>
public sealed record RegisterWithGoogleCommand(
    string IdToken,
    bool AcceptedTerms) : ICommand<Result<GoogleRegisterResult>>;

/// <summary>
/// <paramref name="LinkedToExistingAccount"/> distinguishes the REG-004 outcome (an existing
/// account gained a Google provider) from a genuinely new registration, so the UI can say which
/// happened instead of implying a duplicate account was made.
/// </summary>
public sealed record GoogleRegisterResult(
    Guid UserId,
    string Email,
    bool LinkedToExistingAccount);
