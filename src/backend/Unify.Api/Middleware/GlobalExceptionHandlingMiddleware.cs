using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ValidationException = Unify.Application.Common.Exceptions.ValidationException;

namespace Unify.Api.Middleware;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 problem documents. This is the only place that
/// decides what an exception looks like on the wire, so handlers never construct HTTP results
/// and no stack trace can escape by accident.
/// </summary>
internal sealed class GlobalExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            await WriteValidationProblemAsync(context, ex).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteUnexpectedProblemAsync(context, ex).ConfigureAwait(false);
        }
    }

    private static async Task WriteValidationProblemAsync(HttpContext context, ValidationException ex)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ValidationProblemDetails(ex.Errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Instance = context.Request.Path,
        };

        await WriteProblemAsync(context, problem, StatusCodes.Status400BadRequest).ConfigureAwait(false);
    }

    private async Task WriteUnexpectedProblemAsync(HttpContext context, Exception ex)
    {
        _logger.LogError(
            ex,
            "Unhandled exception for {Method} {Path}.",
            context.Request.Method,
            context.Request.Path);

        if (context.Response.HasStarted)
        {
            // Too late to change the response; the log above is the only record.
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Instance = context.Request.Path,
            // Exception detail is exposed in Development only - never to production clients.
            Detail = _environment.IsDevelopment() ? ex.ToString() : null,
        };

        await WriteProblemAsync(context, problem, StatusCodes.Status500InternalServerError)
            .ConfigureAwait(false);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        ProblemDetails problem,
        int statusCode)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        problem.Extensions["traceId"] = context.TraceIdentifier;

        // Serialize against the runtime type: declaring ProblemDetails here would silently
        // drop the per-field "errors" of a ValidationProblemDetails.
        await context.Response
            .WriteAsync(JsonSerializer.Serialize(problem, problem.GetType(), JsonSerializerOptions.Web))
            .ConfigureAwait(false);
    }
}
