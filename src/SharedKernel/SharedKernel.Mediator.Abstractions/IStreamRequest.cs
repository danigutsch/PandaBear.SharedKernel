using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a request that yields a stream of response items from a single request instance.
/// </summary>
/// <typeparam name="TResponse">The streamed item type.</typeparam>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker stream request contract for generated mediator dispatch.")]
[SuppressMessage("SonarAnalyzer", "S2326:Unused type parameters should be removed", Justification = "The streamed response type parameter defines generated mediator dispatch shape.")]
public interface IStreamRequest<TResponse>;
