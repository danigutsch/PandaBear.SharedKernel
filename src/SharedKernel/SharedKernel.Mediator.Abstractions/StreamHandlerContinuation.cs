namespace SharedKernel.Mediator;

/// <summary>
/// Represents the next step in a generated stream pipeline chain.
/// </summary>
/// <typeparam name="TResponse">The streamed item type produced by the next step.</typeparam>
/// <returns>The response stream produced by the next step.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerContinuation<out TResponse>();
