namespace SharedKernel.DocumentWorkflow;

/// <summary>Binds the version-one review encoding to one lineage, revision, and provenance.</summary>
/// <param name="LineageId">The document lineage.</param>
/// <param name="Number">The revision in that lineage.</param>
/// <param name="Origin">The generation provenance.</param>
/// <param name="Content">The structured and branded content.</param>
internal sealed record DocumentReviewFingerprint(Guid LineageId, int Number, DocumentOrigin Origin, DocumentContent Content);
