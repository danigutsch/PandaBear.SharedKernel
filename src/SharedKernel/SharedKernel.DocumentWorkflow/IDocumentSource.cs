using PandaBear.Results;

namespace PandaBear.DocumentWorkflow;

/// <summary>Reads an application's document source data and its deterministic version.</summary>
/// <typeparam name="TSource">The application-owned source model.</typeparam>
public interface IDocumentSource<TSource>
    where TSource : notnull
{
    /// <summary>Reads the source snapshot used to generate a document.</summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The source data and its version, or the source failure.</returns>
    ValueTask<Result<(string Version, TSource Data)>> Read(Guid sourceId, CancellationToken ct);
}
