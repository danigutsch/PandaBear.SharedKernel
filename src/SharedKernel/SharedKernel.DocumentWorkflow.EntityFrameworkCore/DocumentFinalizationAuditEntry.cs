using SharedKernel.AuditTrail;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Records successful document finalization as metadata in the owning transaction.</summary>
/// <param name="LineageId">The document lineage.</param>
/// <param name="RevisionNumber">The finalized revision within its lineage.</param>
/// <param name="Version">The storage version at finalization.</param>
/// <param name="ActorPseudonym">The pseudonym of the authorized operator.</param>
/// <param name="OccurredAtUtc">The UTC occurrence time from the host clock.</param>
public sealed record DocumentFinalizationAuditEntry(
    Guid LineageId, int RevisionNumber, long Version, string ActorPseudonym, DateTime OccurredAtUtc) : IAuditTrailEntry
{
    /// <inheritdoc />
    public override string ToString() => nameof(DocumentFinalizationAuditEntry);
}
