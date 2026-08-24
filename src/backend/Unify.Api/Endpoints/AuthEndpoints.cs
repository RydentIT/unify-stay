using Unify.Api.Extensions;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.Login;
using Unify.Application.Features.Authentication.RegisterUser;
using Unify.Application.Features.Authentication.RequestPasswordReset;

namespace Unify.Api.Endpoints;

/// <summary>
/// SCAFFOLD. The routes, validation, rate-limit policies and result mapping are real; the
/// handlers behind them are stubs that return 501 until the auth module is built. The shapes
/// here are what packages/api-client is written against.
/// </summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/register", async (
                RegisterUserCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<RegisterUserResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .WithName("Register")
            .WithSummary("Register a new account. NOT IMPLEMENTED - returns 501.")
            .RequireRateLimiting(RateLimitPolicies.Registration);

        group.MapPost("/login", async (
                LoginCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<LoginResult> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("Login")
            .WithSummary("Exchange credentials for an access token. NOT IMPLEMENTED - returns 501.")
            .RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("/forgot-password", async (
                RequestPasswordResetCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset link. NOT IMPLEMENTED - returns 501.")
            .RequireRateLimiting(RateLimitPolicies.PasswordReset);

        return app;
    }
}
