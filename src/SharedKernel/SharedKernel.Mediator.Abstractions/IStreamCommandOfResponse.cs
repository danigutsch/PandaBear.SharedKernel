namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that yields a stream of response items.
/// </summary>
/// <typeparam name="TResponse">The streamed item type.</typeparam>
public interface IStreamCommand<TResponse> : IStreamRequest<TResponse>;
