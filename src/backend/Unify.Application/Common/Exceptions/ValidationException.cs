namespace Unify.Application.Common.Exceptions;

/// <summary>
/// Thrown by <c>ValidationBehavior</c> when a request fails its FluentValidation rules.
/// The API's exception middleware turns this into an RFC 7807 problem document with a
/// per-field <c>errors</c> dictionary, so handlers never validate their own input.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") => Errors = errors;

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
