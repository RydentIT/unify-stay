using Microsoft.AspNetCore.Mvc;
using Unify.Application.Common;

namespace Unify.Api.Extensions;

/// <summary>
/// The single place that translates an application-layer <see cref="Error"/> into an HTTP
/// status code. Handlers stay transport-agnostic; adding an ErrorType means updating this map.
/// </summary>
internal static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    public static IResult ToHttpResult<TValue>(this Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsFailure)
        {
            return Problem(result.Error);
        }

        return successStatusCode == StatusCodes.Status201Created
            ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
            : Results.Json(result.Value, statusCode: successStatusCode);
    }

    private static IResult Problem(Error error)
    {
        int status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
            ErrorType.NotImplemented => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Message,
            Extensions = { ["code"] = error.Code },
        };

        return Results.Problem(problem);
    }
}
