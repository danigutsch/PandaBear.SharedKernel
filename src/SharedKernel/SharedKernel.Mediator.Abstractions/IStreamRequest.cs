namespace SharedKernel.Mediator;

/// <summary>
/// Represents a request that yields a stream of response items from a single request instance.
/// </summary>
/// <typeparam name="TResponse">The streamed item type.</typeparam>
public interface IStreamRequest<TResponse>;
