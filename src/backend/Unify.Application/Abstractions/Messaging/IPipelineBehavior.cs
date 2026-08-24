namespace Unify.Application.Abstractions.Messaging;

/// <summary>Continuation that invokes the next behaviour, ending at the handler itself.</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Cross-cutting concern wrapped around every handler for a given request type.
/// Behaviours run in registration order (first registered is outermost).
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
