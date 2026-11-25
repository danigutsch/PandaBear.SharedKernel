namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Pairs a validated document revision with its storage concurrency version.</summary>
/// <param name="Revision">The validated document workflow revision.</param>
/// <param name="Version">The version to supply for the next write.</param>
public sealed record PersistedDocumentRevision(DocumentRevision Revision, long Version);
