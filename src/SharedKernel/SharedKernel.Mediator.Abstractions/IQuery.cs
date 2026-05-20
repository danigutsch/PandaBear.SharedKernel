using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a query that returns a response value.
/// </summary>
/// <typeparam name="TResponse">The response type returned for the query.</typeparam>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker query contract for generated mediator dispatch.")]
public interface IQuery<TResponse> : IRequest<TResponse>;
