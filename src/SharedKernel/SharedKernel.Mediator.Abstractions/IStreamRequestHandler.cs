namespace SharedKernel.Mediator;

/// <summary>
/// Handles a stream request and produces a stream of response items.
/// </summary>
/// <typeparam name="TRequest">The stream request type handled by the handler.</typeparam>
/// <typeparam name="TResponse">The streamed item type produced by the handler.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the provided stream request.
    /// </summary>
    /// <param name="request">The request instance to handle.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The produced stream of response items.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken ct);
}
