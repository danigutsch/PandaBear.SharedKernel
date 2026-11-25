using SharedKernel.Results;

namespace SharedKernel.DocumentWorkflow;

/// <summary>Durably stores private document artifacts without overwriting existing content.</summary>
public interface IDocumentArtifactStore
{
    /// <summary>Stores the exact bytes and metadata and returns a durable handle.</summary>
    /// <remarks>
    /// A retry with the same lineage, revision, review identity, output digest and media type
    /// returns the same handle. Concurrent writes must enforce immutability atomically.
    /// Consume the supplied memory before returning and never modify it.
    /// </remarks>
    /// <param name="artifact">The renderer output and its identity.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The opaque private handle, or a storage failure.</returns>
    ValueTask<Result<string>> Write(DocumentArtifactWrite artifact, CancellationToken ct);
}
