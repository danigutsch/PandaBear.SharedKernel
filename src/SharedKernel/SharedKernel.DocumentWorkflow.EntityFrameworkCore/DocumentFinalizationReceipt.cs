namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Describes the version and private artifact committed by finalization.</summary>
/// <param name="LineageId">The document lineage.</param>
/// <param name="RevisionNumber">The finalized revision within its lineage.</param>
/// <param name="Version">The storage version at finalization.</param>
/// <param name="ArtifactReference">The opaque handle for host-authorized delivery.</param>
public sealed record DocumentFinalizationReceipt(Guid LineageId, int RevisionNumber, long Version, string ArtifactReference)
{
    /// <inheritdoc />
    public override string ToString() => nameof(DocumentFinalizationReceipt);
}
