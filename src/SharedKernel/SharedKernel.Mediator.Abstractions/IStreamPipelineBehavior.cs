namespace SharedKernel.Mediator;

/// <summary>
/// Defines a pipeline behavior applied around stream request execution.
/// </summary>
/// <typeparam name="TRequest">The stream request type the behavior can handle.</typeparam>
/// <typeparam name="TResponse">The streamed item type produced by the request.</typeparam>
public interface IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the stream request and optionally invokes the next pipeline step.
    /// </summary>
    /// <param name="request">The stream request instance being processed.</param>
    /// <param name="next">The next stream pipeline delegate.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The produced response stream.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerContinuation<TResponse> next, CancellationToken ct);
}
