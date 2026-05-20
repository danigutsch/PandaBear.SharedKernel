namespace SharedKernel.Mediator;

/// <summary>
/// Represents a query that consumes a stream of input items and yields a stream of response items.
/// </summary>
/// <typeparam name="TRequestItem">The streamed input item type.</typeparam>
/// <typeparam name="TResponse">The streamed response item type.</typeparam>
public interface IDuplexStreamQuery<TRequestItem, TResponse> : IStreamQuery<TResponse>
{
    /// <summary>
    /// Gets the streamed input items consumed by the query.
    /// </summary>
    IAsyncEnumerable<TRequestItem> Items { get; }
}
