namespace SharedKernel.Mediator;

/// <summary>
/// Defines a pipeline behavior applied around request execution.
/// </summary>
/// <typeparam name="TRequest">The request type the behavior can handle.</typeparam>
/// <typeparam name="TResponse">The response type produced by the request.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request and optionally invokes the next pipeline step.
    /// </summary>
    /// <param name="request">The request instance being processed.</param>
    /// <param name="next">The next pipeline delegate.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The produced response value.</returns>
    ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct);
}
