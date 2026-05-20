namespace SharedKernel.Mediator;

/// <summary>
/// Represents a query that consumes a stream of input items and resolves to a single response value.
/// </summary>
/// <typeparam name="TRequestItem">The streamed input item type.</typeparam>
/// <typeparam name="TResponse">The response type returned for the query.</typeparam>
public interface IStreamQuery<TRequestItem, TResponse> : IQuery<TResponse>
{
    /// <summary>
    /// Gets the streamed input items consumed by the query.
    /// </summary>
    IAsyncEnumerable<TRequestItem> Items { get; }
}
