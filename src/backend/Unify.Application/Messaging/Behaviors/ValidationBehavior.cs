using FluentValidation;
using FluentValidation.Results;
using Unify.Application.Abstractions.Messaging;
using ValidationException = Unify.Application.Common.Exceptions.ValidationException;

namespace Unify.Application.Messaging.Behaviors;

/// <summary>
/// Runs every registered validator for a request before its handler. Requests with no
/// validator pass straight through, so adding a validator is the only step needed to start
/// validating a new use case.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) =>
        _validators = validators;

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validators = _validators as IValidator<TRequest>[] ?? _validators.ToArray();

        if (validators.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);

        ValidationResult[] results = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false);

        ValidationFailure[] failures = [.. results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)];

        if (failures.Length > 0)
        {
            Dictionary<string, string[]> errors = failures
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal);

            throw new ValidationException(errors);
        }

        return await next().ConfigureAwait(false);
    }
}
