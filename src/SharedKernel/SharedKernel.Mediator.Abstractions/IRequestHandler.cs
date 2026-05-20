namespace SharedKernel.Mediator;

/// <summary>
/// Handles a request and produces a response value.
/// </summary>
/// <typeparam name="TRequest">The request type handled by the handler.</typeparam>
/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the provided request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The produced response value.</returns>
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct);
}
