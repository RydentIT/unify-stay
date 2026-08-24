namespace Unify.Application.Abstractions.Messaging;

/// <summary>
/// Marker for anything the dispatcher can route to exactly one handler.
/// Prefer the <see cref="ICommand{TResponse}"/> / <see cref="IQuery{TResponse}"/> aliases
/// at call sites - they say whether the operation is expected to mutate state.
/// </summary>
public interface IRequest<out TResponse>;

/// <summary>A state-changing use case.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A read-only use case.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
