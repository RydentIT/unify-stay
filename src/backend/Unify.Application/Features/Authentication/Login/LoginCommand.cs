using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// The outcome of a successful sign-in.
///
/// When <paramref name="MustChangePassword"/> or <paramref name="MustCompleteProfile"/> is true
/// the access token is a limited-scope credential (LOG-013 / the Google profile-completion
/// equivalent): it authenticates the user but authorises nothing except the corresponding
/// recovery endpoint, and no refresh token is issued at all. The two are mutually exclusive -
/// only an email-registered account can owe a password change, only a Google-registered one can
/// owe a phone number.
/// </summary>
public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    bool MustChangePassword,
    bool MustCompleteProfile,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAt,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// <paramref name="RememberMe"/> extends the REFRESH token lifetime only. The access token
/// lifetime is fixed regardless (BR-LOG-005).
/// </summary>
public sealed record LoginWithEmailCommand(
    string Email,
    string Password,
    bool RememberMe = false) : ICommand<Result<LoginResult>>;

/// <summary>Google sign-in. Restricted to Student/PropertyOwner accounts (LOG-016).</summary>
public sealed record LoginWithGoogleCommand(
    string IdToken,
    bool RememberMe = false) : ICommand<Result<LoginResult>>;
