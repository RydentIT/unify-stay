using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Options;
using Unify.Domain.Authentication;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Login;

/// <summary>
/// Turns an authenticated user into the tokens the client gets back. Shared by the email and
/// Google login handlers so the forced-reset rule and the Remember Me rule cannot drift apart
/// between the two entry points.
/// </summary>
internal sealed class LoginSessionIssuer
{
    private readonly ITokenService _tokenService;
    private readonly ISessionRepository _sessions;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IDateTimeProvider _clock;
    private readonly AuthOptions _auth;

    public LoginSessionIssuer(
        ITokenService tokenService,
        ISessionRepository sessions,
        ISecureTokenGenerator tokenGenerator,
        IDateTimeProvider clock,
        IOptions<AuthOptions> auth)
    {
        ArgumentNullException.ThrowIfNull(auth);

        _tokenService = tokenService;
        _sessions = sessions;
        _tokenGenerator = tokenGenerator;
        _clock = clock;
        _auth = auth.Value;
    }

    public async Task<LoginResult> IssueAsync(User user, bool rememberMe, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;

        // LOG-012/LOG-013 and its Google-profile equivalent are both checked before anything
        // else is issued. Either limited-scope token gets NO refresh token and NO role claims,
        // so even if a route forgot its policy there is nothing on the token to authorise
        // against. The two flags cannot both be true - see the LoginResult doc comment - so
        // checking one first does not risk masking the other.
        if (user.MustChangePassword)
        {
            AccessToken limited = _tokenService.CreateAccessToken(
                user.Id,
                user.Email.Value,
                roles: [],
                TokenScope.PasswordChangeRequired);

            return new LoginResult(
                limited.Value,
                limited.ExpiresAt,
                MustChangePassword: true,
                MustCompleteProfile: false,
                RefreshToken: null,
                RefreshTokenExpiresAt: null,
                Roles: []);
        }

        if (user.MustCompleteProfile)
        {
            AccessToken limited = _tokenService.CreateAccessToken(
                user.Id,
                user.Email.Value,
                roles: [],
                TokenScope.ProfileCompletionRequired);

            return new LoginResult(
                limited.Value,
                limited.ExpiresAt,
                MustChangePassword: false,
                MustCompleteProfile: true,
                RefreshToken: null,
                RefreshTokenExpiresAt: null,
                Roles: []);
        }

        AccessToken access = _tokenService.CreateAccessToken(
            user.Id,
            user.Email.Value,
            user.Roles.ToArray(),
            TokenScope.Full);

        // BR-LOG-005: Remember Me lengthens the refresh window only. The access token above was
        // already built with its fixed lifetime and is unaffected by this choice.
        int refreshDays = rememberMe
            ? _auth.RememberMeRefreshTokenLifetimeDays
            : _auth.RefreshTokenLifetimeDays;

        DateTimeOffset refreshExpiresAt = now.AddDays(refreshDays);
        GeneratedToken refresh = _tokenGenerator.Generate();

        var session = new Session(
            Guid.CreateVersion7(),
            user.Id,
            refresh.Hash,
            user.Roles.Count > 0 ? user.Roles.First() : null,
            refreshExpiresAt,
            now);

        await _sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        return new LoginResult(
            access.Value,
            access.ExpiresAt,
            MustChangePassword: false,
            MustCompleteProfile: false,
            RefreshToken: refresh.RawValue,
            RefreshTokenExpiresAt: refreshExpiresAt,
            Roles: [.. user.Roles.Select(role => role.ToString())]);
    }
}
