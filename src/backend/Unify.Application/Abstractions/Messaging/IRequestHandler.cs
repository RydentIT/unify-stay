namespace Unify.Application.Abstractions.Messaging;

/// <summary>
/// One handler per use case. Handlers are registered in DI by assembly scan (see
/// <c>DependencyInjection.AddApplication</c>) and resolved by the dispatcher.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
