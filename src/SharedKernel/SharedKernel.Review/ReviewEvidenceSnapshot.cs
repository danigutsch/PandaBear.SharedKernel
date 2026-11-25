namespace SharedKernel.Review;

/// <summary>Represents one stored evidence entry awaiting validation on restore.</summary>
/// <param name="Revision">The content revision that was reviewed.</param>
/// <param name="Lane">The host-defined lane.</param>
/// <param name="Decision">The human decision or automated advisory.</param>
/// <param name="ActorPseudonym">The host-supplied pseudonym.</param>
/// <param name="OccurredAt">The recorded time.</param>
/// <param name="ReasonCode">The bounded host-defined reason.</param>
public sealed record ReviewEvidenceSnapshot(
    ReviewRevision Revision,
    string Lane,
    ReviewDecision Decision,
    string ActorPseudonym,
    DateTimeOffset OccurredAt,
    string ReasonCode)
{
    /// <summary>Returns a diagnostic representation without review contents.</summary>
    /// <returns>The redacted representation.</returns>
    public override string ToString() => $"{nameof(ReviewEvidenceSnapshot)} [redacted]";
}
