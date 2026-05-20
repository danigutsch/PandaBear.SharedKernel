using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a query that yields a stream of response items.
/// </summary>
/// <typeparam name="TResponse">The streamed item type.</typeparam>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Semantic alias for stream-producing queries.")]
public interface IStreamQuery<TResponse> : IStreamRequest<TResponse>;
