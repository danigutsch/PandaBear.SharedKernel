namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that consumes a stream of input items and yields a stream of response items.
/// </summary>
/// <typeparam name="TRequestItem">The streamed input item type.</typeparam>
/// <typeparam name="TResponse">The streamed response item type.</typeparam>
public interface IDuplexStreamCommand<TRequestItem, TResponse> : IStreamCommand<TResponse>
{
    /// <summary>
    /// Gets the streamed input items consumed by the command.
    /// </summary>
    IAsyncEnumerable<TRequestItem> Items { get; }
}
