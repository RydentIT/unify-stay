using Microsoft.AspNetCore.Mvc;
using Unify.Api.Extensions;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.EmailVerification;
using Unify.Application.Features.Authentication.Login;
using Unify.Application.Features.Authentication.Password;
using Unify.Application.Features.Authentication.Register;

namespace Unify.Api.Endpoints;

/// <summary>
/// Routes under /api/auth.
///
/// Everything here is anonymous except logout and the forced change-password call. The latter
/// is the single route a limited-scope token may reach (LOG-013/BR-LOG-008), which is why it
/// carries the PasswordChangeOnly policy rather than the default full-access one.
/// </summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/register", async (
                RegisterWithEmailCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<RegisterResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .WithName("Register")
            .WithSummary("Register with email and password.")
            .RequireRateLimiting(RateLimitPolicies.Registration);

        group.MapPost("/google/register", async (
                RegisterWithGoogleCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<GoogleRegisterResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .WithName("RegisterWithGoogle")
            .WithSummary("Register or link an account using a Google ID token.")
            .RequireRateLimiting(RateLimitPolicies.Registration);

        group.MapPost("/login", async (
                LoginWithEmailCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<LoginResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("Login")
            .WithSummary("Exchange credentials for an access token.")
            .RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("/google/login", async (
                LoginWithGoogleCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<LoginResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("LoginWithGoogle")
            .WithSummary("Sign in with a Google ID token. Student and Property Owner only.")
            .RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("/logout", async (
                LogoutCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithName("Logout")
            .WithSummary("Revoke the session server-side.")
            // Deliberately allows either limited-scope token too: a user part-way through a
            // forced reset or a Google profile completion must still be able to sign out.
            .RequireAuthorization(AuthorizationPolicies.AnyScope);

        // The one route reachable with a limited-scope (password-change) token.
        group.MapPost("/change-password-required", async (
                ChangePasswordForcedCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<LoginResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("ChangePasswordRequired")
            .WithSummary("Complete the mandatory password change and receive a full-access token.")
            .RequireAuthorization(AuthorizationPolicies.PasswordChangeOnly);

        // The one route reachable with a limited-scope (profile-completion) token.
        group.MapPost("/complete-profile", async (
                CompleteGoogleProfileCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<LoginResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("CompleteGoogleProfile")
            .WithSummary("Supply the phone number a Google account is missing and receive a full-access token.")
            .RequireAuthorization(AuthorizationPolicies.ProfileCompletionOnly);

        group.MapPost("/forgot-password", async (
                RequestPasswordResetCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);

                // Always 202 on the happy path: the handler reports success even for unknown
                // addresses (BR-FPW-002), and the status code must not undo that.
                return result.IsSuccess ? Results.Accepted() : result.ToHttpResult();
            })
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset link.")
            .RequireRateLimiting(RateLimitPolicies.PasswordReset);

        group.MapPost("/reset-password", async (
                ResetPasswordCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithName("ResetPassword")
            .WithSummary("Set a new password using a reset token.")
            .RequireRateLimiting(RateLimitPolicies.PasswordReset);

        group.MapPost("/verify-email", async (
                VerifyEmailCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithName("VerifyEmail")
            .WithSummary("Confirm an email address using a verification token.");

        group.MapPost("/resend-verification", async (
                ResendVerificationEmailCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.IsSuccess ? Results.Accepted() : result.ToHttpResult();
            })
            .WithName("ResendVerification")
            .WithSummary("Resend the verification email.")
            .RequireRateLimiting(RateLimitPolicies.ResendVerification);

        return app;
    }
}
