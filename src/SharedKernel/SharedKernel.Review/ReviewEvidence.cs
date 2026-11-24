namespace PandaBear.Review;

/// <summary>Captures an immutable human decision or automated advisory about a content revision.</summary>
public sealed class ReviewEvidence
{
    /// <summary>Creates the immutable record after the history checks the decision.</summary>
    /// <param name="revision">The reviewed content.</param>
    /// <param name="lane">The configured review lane.</param>
    /// <param name="decision">The accepted decision.</param>
    /// <param name="actorPseudonym">The host-supplied reviewer pseudonym.</param>
    /// <param name="occurredAt">The time of the decision.</param>
    /// <param name="reasonCode">The host-defined reason identifier.</param>
    internal ReviewEvidence(
        ReviewRevision revision,
        string lane,
        ReviewDecision decision,
        string actorPseudonym,
        DateTimeOffset occurredAt,
        string reasonCode)
    {
        Revision = revision;
        Lane = lane;
        Decision = decision;
        ActorPseudonym = actorPseudonym;
        OccurredAt = occurredAt;
        ReasonCode = reasonCode;
    }

    /// <summary>Gets the version of the content the reviewer saw.</summary>
    public ReviewRevision Revision { get; }

    /// <summary>Gets the host-defined lane identifier.</summary>
    public string Lane { get; }

    /// <summary>Gets the review decision. Advisory entries have no authority.</summary>
    public ReviewDecision Decision { get; }

    /// <summary>Gets the host-supplied pseudonym of the human or automated reviewer.</summary>
    public string ActorPseudonym { get; }

    /// <summary>Gets the time the host recorded for this evidence.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Gets the bounded, host-defined reason identifier.</summary>
    public string ReasonCode { get; }
}
