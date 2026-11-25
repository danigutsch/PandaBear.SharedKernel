using PandaBear.Review;

namespace PandaBear.DocumentWorkflow;

/// <summary>Supplies the typed state that a host persists for a document revision.</summary>
/// <param name="LineageId">The lineage identity.</param>
/// <param name="Number">The revision number within the lineage.</param>
/// <param name="Origin">The captured source and template provenance.</param>
/// <param name="Content">The structured content and branding.</param>
/// <param name="Review">The ordered review evidence.</param>
/// <param name="Status">The stored lifecycle state.</param>
/// <param name="ArtifactReference">The private handle to a finalized artifact, when present.</param>
public sealed record DocumentRevisionSnapshot(
    Guid LineageId,
    int Number,
    DocumentOrigin Origin,
    DocumentContent Content,
    ReviewHistorySnapshot Review,
    DocumentLifecycleStatus Status,
    string? ArtifactReference)
{
    /// <summary>Returns a diagnostic representation without private content.</summary>
    /// <returns>The redacted representation.</returns>
    public override string ToString() => $"{nameof(DocumentRevisionSnapshot)} [redacted]";
}
