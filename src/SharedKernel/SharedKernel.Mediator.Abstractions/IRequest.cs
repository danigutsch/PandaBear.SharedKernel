using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a request that resolves to a response value.
/// </summary>
/// <typeparam name="TResponse">The response type returned for the request.</typeparam>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker request contract for generated mediator dispatch.")]
[SuppressMessage("SonarAnalyzer", "S2326:Unused type parameters should be removed", Justification = "The response type parameter defines generated mediator dispatch shape.")]
public interface IRequest<TResponse>;
