using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Security;

namespace Unify.Infrastructure.Security;

/// <summary>
/// Verifies a Google ID token against Google's published signing keys.
///
/// Everything the caller learns about the user comes out of the validated token payload. The
/// browser sends only the token, so a tampered email or email_verified flag cannot survive the
/// signature check - which matters because REG-004 hands over an existing account on the
/// strength of that flag.
/// </summary>
internal sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleOptions _options;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IOptions<GoogleOptions> options, ILogger<GoogleTokenValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogError(
                "Google sign-in was attempted but Google:ClientId is not configured. Set the Google__ClientId environment variable.");

            return null;
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                // Restricting the audience is what stops a token minted for some other
                // application from being replayed against ours.
                Audience = [_options.ClientId],
                IssuedAtClockTolerance = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
                ExpirationTimeClockTolerance = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
            };

            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature
                .ValidateAsync(idToken, settings)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.Name,
                payload.GivenName,
                payload.FamilyName);
        }
        catch (InvalidJwtException ex)
        {
            // Expected for a forged, expired or wrong-audience token: not an error condition.
            _logger.LogInformation(ex, "Rejected an invalid Google ID token.");
            return null;
        }
    }
}
