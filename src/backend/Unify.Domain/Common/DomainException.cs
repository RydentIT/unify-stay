namespace Unify.Domain.Common;

/// <summary>
/// Raised when an operation would leave a domain object in an invalid state. These are
/// programming/consistency errors, not user input errors - user input is rejected by the
/// FluentValidation validators in Unify.Application before a handler ever runs.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
