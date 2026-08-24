namespace Unify.Application.Common;

/// <summary>
/// Transport-agnostic failure category. The API layer owns the mapping from these to HTTP
/// status codes, so handlers never mention HTTP.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    RateLimited,
    NotImplemented,
    Unexpected,
}

/// <summary>A machine-readable failure. <paramref name="Code"/> is stable; message is not.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Unexpected);

    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) =>
        new(code, message, ErrorType.Unauthorized);

    /// <summary>
    /// Used by the stub handlers that exist purely to prove the layers are wired together.
    /// Every one of these is expected to disappear as the real modules land.
    /// </summary>
    public static Error NotImplemented(string code, string message) =>
        new(code, message, ErrorType.NotImplemented);
}
