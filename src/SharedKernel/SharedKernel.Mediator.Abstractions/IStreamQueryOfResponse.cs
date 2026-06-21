namespace SharedKernel.Mediator;

/// <summary>
/// Represents a query that yields a stream of response items.
/// </summary>
/// <typeparam name="TResponse">The streamed item type.</typeparam>
public interface IStreamQuery<TResponse> : IStreamRequest<TResponse>;
