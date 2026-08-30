using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Unify.Api.Extensions;

internal static class RateLimitingExtensions
{
    /// <summary>
    /// Registers the built-in .NET rate limiter with a global fallback plus the three named
    /// policies the auth module needs. Partitions are per-caller, so one abusive client cannot
    /// exhaust the budget for everyone.
    ///
    /// NOTE: these are in-memory partitions, i.e. per API instance. Once the deployment target
    /// is chosen and the API runs more than one replica, this needs a distributed counter
    /// (typically Redis) or the effective limit becomes limit x replica count.
    /// </summary>
    public static IServiceCollection AddUnifyRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(limiter =>
        {
            var options = configuration
                .GetSection(RateLimitingOptions.SectionName)
                .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => CreatePartition(context, options.Global));

            limiter.AddPolicy(
                RateLimitPolicies.Registration,
                context => CreatePartition(context, options.Registration));

            limiter.AddPolicy(
                RateLimitPolicies.Login,
                context => CreatePartition(context, options.Login));

            limiter.AddPolicy(
                RateLimitPolicies.PasswordReset,
                context => CreatePartition(context, options.PasswordReset));

            limiter.AddPolicy(
                RateLimitPolicies.ResendVerification,
                context => CreatePartition(context, options.ResendVerification));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsync(
                    """
                    {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.29",
                     "title":"Too Many Requests","status":429,
                     "detail":"Rate limit exceeded. Please retry later."}
                    """,
                    cancellationToken).ConfigureAwait(false);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext context, RateLimitWindow window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = window.PermitLimit,
                Window = TimeSpan.FromMinutes(window.WindowMinutes),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });

    /// <summary>
    /// Authenticated callers are partitioned by user id; everyone else by remote IP.
    /// Requests with no determinable IP share one bucket rather than bypassing the limit.
    /// </summary>
    private static string PartitionKey(HttpContext context)
    {
        string? userId = context.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
