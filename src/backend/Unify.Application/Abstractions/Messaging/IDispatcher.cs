namespace Unify.Application.Abstractions.Messaging;

/// <summary>
/// Routes a request to its single registered handler, through any pipeline behaviours.
/// The API layer depends on this rather than on individual handlers, which keeps endpoint
/// wiring uniform and makes the handler set easy to grow.
/// </summary>
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
