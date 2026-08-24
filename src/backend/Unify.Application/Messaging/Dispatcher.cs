using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Unify.Application.Abstractions.Messaging;

namespace Unify.Application.Messaging;

/// <summary>
/// Minimal in-process request dispatcher. Deliberately hand-rolled rather than taking a
/// mediator package dependency; the interfaces mirror the common shape closely enough that
/// swapping in a library later is a mechanical change.
/// </summary>
internal sealed class Dispatcher : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> ExecutorCache = new();

    private readonly IServiceProvider _services;

    public Dispatcher(IServiceProvider services) => _services = services;

    public Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Cache keyed on the concrete request type: the closed generic executor is what
        // recovers the static TRequest that handler/behaviour resolution needs.
        var executor = (RequestExecutor<TResponse>)ExecutorCache.GetOrAdd(
            request.GetType(),
            static requestType =>
            {
                Type executorType = typeof(RequestExecutor<,>)
                    .MakeGenericType(requestType, typeof(TResponse));

                return Activator.CreateInstance(executorType)
                    ?? throw new InvalidOperationException(
                        $"Could not create a dispatcher executor for '{requestType}'.");
            });

        return executor.ExecuteAsync(_services, request, cancellationToken);
    }

    private abstract class RequestExecutor<TResponse>
    {
        public abstract Task<TResponse> ExecuteAsync(
            IServiceProvider services,
            IRequest<TResponse> request,
            CancellationToken cancellationToken);
    }

    private sealed class RequestExecutor<TRequest, TResponse> : RequestExecutor<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> ExecuteAsync(
            IServiceProvider services,
            IRequest<TResponse> request,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            // The handler is resolved inside the innermost delegate, not before the pipeline
            // is built. A request rejected by validation therefore never constructs its
            // handler - and never opens the connections that handler's dependencies would.
            RequestHandlerDelegate<TResponse> pipeline = () =>
            {
                var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
                    ?? throw new InvalidOperationException(
                        $"No handler is registered for request type '{typeof(TRequest)}'. " +
                        "Handlers are discovered by assembly scan - check that the handler is " +
                        "non-abstract and lives in the Unify.Application assembly.");

                return handler.HandleAsync(typedRequest, cancellationToken);
            };

            // Wrap in reverse so the first-registered behaviour ends up outermost.
            var behaviours = services
                .GetServices<IPipelineBehavior<TRequest, TResponse>>()
                .Reverse();

            foreach (var behaviour in behaviours)
            {
                RequestHandlerDelegate<TResponse> next = pipeline;
                pipeline = () => behaviour.HandleAsync(typedRequest, next, cancellationToken);
            }

            return pipeline();
        }
    }
}
