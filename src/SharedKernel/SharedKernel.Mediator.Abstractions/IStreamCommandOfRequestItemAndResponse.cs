namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that consumes a stream of input items and resolves to a single response value.
/// </summary>
/// <typeparam name="TRequestItem">The streamed input item type.</typeparam>
/// <typeparam name="TResponse">The response type returned for the command.</typeparam>
public interface IStreamCommand<TRequestItem, TResponse> : ICommand<TResponse>
{
    /// <summary>
    /// Gets the streamed input items consumed by the command.
    /// </summary>
    IAsyncEnumerable<TRequestItem> Items { get; }
}
