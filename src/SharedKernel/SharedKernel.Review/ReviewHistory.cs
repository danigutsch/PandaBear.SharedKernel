using System.Collections.Immutable;
using SharedKernel.Results;

namespace SharedKernel.Review;

/// <summary>Tracks ordered human decisions and append-only advisory evidence for versioned content.</summary>
public sealed class ReviewHistory
{
    /// <summary>The maximum length of a reason identifier.</summary>
    public const int MaximumReasonLength = 128;

    private const int MaximumIdentifierLength = 128;
    private readonly int approvedLaneCount;

    private ReviewHistory(
        ReviewRevision revision,
        ImmutableArray<string> lanes,
        ImmutableList<ReviewEvidence> entries,
        int approvedLaneCount,
        bool changesRequested)
    {
        Revision = revision;
        Lanes = lanes;
        Entries = entries;
        this.approvedLaneCount = approvedLaneCount;
        ChangesRequested = changesRequested;
    }

    /// <summary>Gets the current content revision.</summary>
    public ReviewRevision Revision { get; }

    /// <summary>Gets the lane identifiers in the order human approval requires.</summary>
    public ImmutableArray<string> Lanes { get; }

    /// <summary>Gets the recorded evidence across all revisions in append order.</summary>
    public ImmutableList<ReviewEvidence> Entries { get; }

    /// <summary>Gets whether all human lanes have approved this revision.</summary>
    public bool IsApproved => approvedLaneCount == Lanes.Length;

    /// <summary>Gets whether a human has requested changes to this revision.</summary>
    public bool ChangesRequested { get; }

    /// <summary>Gets the lane that may decide next, or null when human review has ended.</summary>
    public string? ExpectedHumanLane => ChangesRequested || IsApproved ? null : Lanes[approvedLaneCount];

    /// <summary>Captures the current version and all historical evidence for durable storage.</summary>
    /// <returns>The typed snapshot.</returns>
    public ReviewHistorySnapshot Snapshot() =>
        new(Revision, Lanes, [.. Entries.Select(static entry => new ReviewEvidenceSnapshot(
            entry.Revision, entry.Lane, entry.Decision, entry.ActorPseudonym, entry.OccurredAt, entry.ReasonCode))]);

    /// <summary>Reconstructs a review history by replaying its ordered evidence.</summary>
    /// <param name="snapshot">The persisted current version, lanes, and evidence.</param>
    /// <returns>The validated history, or an invalid result when the state cannot be replayed.</returns>
    public static Result<ReviewHistory> Restore(ReviewHistorySnapshot? snapshot)
    {
        if (snapshot?.Revision is null || snapshot.Lanes.IsDefaultOrEmpty || snapshot.Evidence.IsDefault)
        {
            return InvalidSnapshot();
        }

        var initial = snapshot.Evidence.IsEmpty ? snapshot.Revision : snapshot.Evidence[0]?.Revision;
        if (initial is null)
        {
            return InvalidSnapshot();
        }

        try
        {
            var history = Start(initial, snapshot.Lanes);
            foreach (var evidence in snapshot.Evidence)
            {
                if (evidence?.Revision is null || evidence.Revision.Number > snapshot.Revision.Number)
                {
                    return InvalidSnapshot();
                }

                if (evidence.Revision.Number > history.Revision.Number)
                {
                    history = history.StartRevision(evidence.Revision).Value;
                }

                var replayed = evidence.Decision == ReviewDecision.Advisory
                    ? history.RecordAdvisory(evidence.Revision, evidence.Lane, evidence.ActorPseudonym, evidence.OccurredAt, evidence.ReasonCode)
                    : history.RecordHumanDecision(evidence.Revision, evidence.Lane, evidence.Decision,
                        evidence.ActorPseudonym, evidence.OccurredAt, evidence.ReasonCode);
                if (replayed.IsFailure)
                {
                    return InvalidSnapshot();
                }

                history = replayed.Value;
            }

            if (history.Revision == snapshot.Revision)
            {
                return Result.Ok(history);
            }

            var current = history.StartRevision(snapshot.Revision);
            return current.IsFailure ? InvalidSnapshot() : current;
        }
        catch (ArgumentException)
        {
            return InvalidSnapshot();
        }
    }

    /// <summary>Starts a review with the given sequence of human lanes.</summary>
    /// <param name="revision">The first content version.</param>
    /// <param name="lanes">Distinct lane identifiers in approval order.</param>
    /// <returns>The empty review history.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="revision"/> or <paramref name="lanes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the lane sequence is empty or contains invalid or repeated identifiers.</exception>
    public static ReviewHistory Start(ReviewRevision revision, IEnumerable<string> lanes)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(lanes);

        var orderedLanes = lanes.ToImmutableArray();
        if (orderedLanes.IsEmpty || orderedLanes.Any(static lane => !IsBounded(lane, MaximumIdentifierLength)) ||
            orderedLanes.Distinct(StringComparer.Ordinal).Count() != orderedLanes.Length)
        {
            throw new ArgumentException("Review requires at least one distinct, nonblank lane identifier.", nameof(lanes));
        }

        return new ReviewHistory(revision, orderedLanes, ImmutableList<ReviewEvidence>.Empty, 0, changesRequested: false);
    }

    /// <summary>Starts a new content revision and requires fresh approval in every lane.</summary>
    /// <param name="revision">The new content version with a greater number.</param>
    /// <returns>The new review history, or a conflict when the number has already been used.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="revision"/> is null.</exception>
    public Result<ReviewHistory> StartRevision(ReviewRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        return revision.Number > Revision.Number
            ? Result.Ok(new ReviewHistory(revision, Lanes, Entries, 0, changesRequested: false))
            : Result.Conflict<ReviewHistory>("A new review revision must advance the revision number.");
    }

    /// <summary>Records a decision from a human reviewer the host has authorized for the expected lane.</summary>
    /// <param name="revision">The content the human reviewed.</param>
    /// <param name="lane">The next lane in the configured order.</param>
    /// <param name="decision">Human approval or a request for changes.</param>
    /// <param name="actorPseudonym">The host-supplied pseudonym of the reviewer.</param>
    /// <param name="occurredAt">The time of the decision.</param>
    /// <param name="reasonCode">The host-defined reason identifier.</param>
    /// <returns>The new history, or a failure when the decision or review state is invalid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="revision"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a lane, actor pseudonym, or reason identifier is blank or too long.</exception>
    public Result<ReviewHistory> RecordHumanDecision(
        ReviewRevision revision,
        string lane,
        ReviewDecision decision,
        string actorPseudonym,
        DateTimeOffset occurredAt,
        string reasonCode)
    {
        ValidateEvidence(revision, lane, actorPseudonym, reasonCode);
        if (decision is not (ReviewDecision.Approved or ReviewDecision.ChangesRequested))
        {
            return Result.Invalid<ReviewHistory>("A human decision must approve or request changes.", nameof(decision), "Unsupported human decision.");
        }

        if (revision != Revision)
        {
            return Result.Conflict<ReviewHistory>("The review decision targets a stale content revision.");
        }

        if (!string.Equals(lane, ExpectedHumanLane, StringComparison.Ordinal))
        {
            return Result.Conflict<ReviewHistory>("The lane is not open for a human decision.");
        }

        var entry = new ReviewEvidence(revision, lane, decision, actorPseudonym, occurredAt, reasonCode);
        return Result.Ok(decision == ReviewDecision.Approved
            ? Append(entry, approvedLaneCount + 1, changesRequested: false)
            : Append(entry, approvedLaneCount, changesRequested: true));
    }

    /// <summary>Records a finding from automated review without advancing human approval.</summary>
    /// <param name="revision">The content the automated reviewer analyzed.</param>
    /// <param name="lane">A configured lane relevant to the finding.</param>
    /// <param name="actorPseudonym">The host-supplied pseudonym for the automated reviewer.</param>
    /// <param name="occurredAt">The time of the finding.</param>
    /// <param name="reasonCode">The host-defined reason identifier.</param>
    /// <returns>The new history, or a failure when the revision or lane is invalid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="revision"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a lane, actor pseudonym, or reason identifier is blank or too long.</exception>
    public Result<ReviewHistory> RecordAdvisory(
        ReviewRevision revision,
        string lane,
        string actorPseudonym,
        DateTimeOffset occurredAt,
        string reasonCode)
    {
        ValidateEvidence(revision, lane, actorPseudonym, reasonCode);
        if (revision != Revision)
        {
            return Result.Conflict<ReviewHistory>("The advisory targets a stale content revision.");
        }

        if (!Lanes.Contains(lane, StringComparer.Ordinal))
        {
            return Result.Invalid<ReviewHistory>("The advisory must target a configured lane.", nameof(lane), "Unknown review lane.");
        }

        var entry = new ReviewEvidence(revision, lane, ReviewDecision.Advisory, actorPseudonym, occurredAt, reasonCode);
        return Result.Ok(Append(entry, approvedLaneCount, ChangesRequested));
    }

    private static Result<ReviewHistory> InvalidSnapshot() =>
        Result.Invalid<ReviewHistory>("Stored review evidence is invalid.", "snapshot", "The review evidence could not be replayed.");

    private ReviewHistory Append(ReviewEvidence entry, int approvedCount, bool changesRequested) =>
        new(Revision, Lanes, Entries.Add(entry), approvedCount, changesRequested);

    private static void ValidateEvidence(ReviewRevision revision, string lane, string actorPseudonym, string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!IsBounded(lane, MaximumIdentifierLength))
        {
            throw new ArgumentException("A bounded lane identifier is required.", nameof(lane));
        }

        if (!IsBounded(actorPseudonym, MaximumIdentifierLength))
        {
            throw new ArgumentException("A bounded actor pseudonym is required.", nameof(actorPseudonym));
        }

        if (!IsBounded(reasonCode, MaximumReasonLength))
        {
            throw new ArgumentException("A bounded reason identifier is required.", nameof(reasonCode));
        }
    }

    private static bool IsBounded(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= limit;
}
