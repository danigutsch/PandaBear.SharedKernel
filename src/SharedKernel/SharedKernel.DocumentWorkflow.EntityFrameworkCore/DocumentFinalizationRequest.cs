using SharedKernel.Idempotency;
using SharedKernel.Review;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Finalizes the exact approved revision identified by a host-authorized operator.</summary>
/// <param name="LineageId">The document lineage.</param>
/// <param name="RevisionNumber">The document revision within its lineage.</param>
/// <param name="ExpectedVersion">The observed optimistic storage version.</param>
/// <param name="Reviewed">The approved review revision.</param>
/// <param name="ActorPseudonym">The host-derived pseudonym of the authorized operator.</param>
/// <param name="IdempotencyKey">The high-entropy key that identifies delivery retries.</param>
public sealed record DocumentFinalizationRequest(
    Guid LineageId,
    int RevisionNumber,
    long ExpectedVersion,
    ReviewRevision Reviewed,
    string ActorPseudonym,
    IdempotencyKey IdempotencyKey)
{
    /// <inheritdoc />
    public override string ToString() => nameof(DocumentFinalizationRequest);
}
