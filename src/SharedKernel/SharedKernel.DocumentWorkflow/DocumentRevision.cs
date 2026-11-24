using PandaBear.Results;
using PandaBear.Review;

namespace PandaBear.DocumentWorkflow;

/// <summary>Tracks the lifecycle and review evidence of one immutable document revision snapshot.</summary>
public sealed record DocumentRevision
{
    private DocumentRevision(Guid lineageId, int number, DocumentOrigin origin, DocumentContent content, ReviewHistory review)
    {
        LineageId = lineageId;
        Number = number;
        Origin = origin;
        Content = content;
        Review = review;
    }

    /// <summary>Gets the identity shared by all revisions of the document.</summary>
    public Guid LineageId { get; }

    /// <summary>Gets the revision number within the lineage.</summary>
    public int Number { get; }

    /// <summary>Gets the source and template provenance captured at generation.</summary>
    public DocumentOrigin Origin { get; }

    /// <summary>Gets the structured content ready to present to a reviewer.</summary>
    public DocumentContent Content { get; private init; }

    /// <summary>Gets the ordered review evidence and current content fingerprint.</summary>
    public ReviewHistory Review { get; private init; }

    /// <summary>Gets the lifecycle phase.</summary>
    public DocumentLifecycleStatus Status { get; private init; } = DocumentLifecycleStatus.DraftGenerated;

    /// <summary>Gets the immutable artifact handle after finalization, including historical revisions.</summary>
    public string? ArtifactReference { get; private init; }

    /// <summary>Gets whether this revision can be replaced.</summary>
    internal bool CanRegenerate => Status is not (DocumentLifecycleStatus.Superseded or DocumentLifecycleStatus.Voided);

    /// <summary>Captures the structured revision and its reviewed history for durable storage.</summary>
    /// <returns>The typed snapshot.</returns>
    public DocumentRevisionSnapshot Snapshot() =>
        new(LineageId, Number, Origin, Content, Review.Snapshot(), Status, ArtifactReference);

    /// <summary>Reconstructs a document revision after validating its complete stored snapshot.</summary>
    /// <param name="snapshot">The stored revision data.</param>
    /// <returns>The validated revision, or an invalid result for inconsistent state.</returns>
    public static Result<DocumentRevision> Restore(DocumentRevisionSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.LineageId == Guid.Empty || snapshot.Number <= 0 ||
            snapshot.Origin is null || snapshot.Origin.SourceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.Origin.SourceVersion) ||
            string.IsNullOrWhiteSpace(snapshot.Origin.TemplateId) ||
            string.IsNullOrWhiteSpace(snapshot.Origin.TemplateVersion) || snapshot.Content is null ||
            (snapshot.ArtifactReference is not null && string.IsNullOrWhiteSpace(snapshot.ArtifactReference)))
        {
            return InvalidSnapshot();
        }

        var restored = ReviewHistory.Restore(snapshot.Review);
        if (restored.IsFailure)
        {
            return InvalidSnapshot();
        }

        var review = restored.Value;
        var digest = snapshot.Content.CalculateHash(snapshot.LineageId, snapshot.Number, snapshot.Origin);
        if (!string.Equals(digest, review.Revision.ContentHash, StringComparison.Ordinal) || !HasConsistentState(snapshot, review))
        {
            return InvalidSnapshot();
        }

        return Result.Ok(new DocumentRevision(snapshot.LineageId, snapshot.Number, snapshot.Origin, snapshot.Content, review)
        {
            Status = snapshot.Status,
            ArtifactReference = snapshot.ArtifactReference,
        });
    }

    /// <summary>Starts human review of a generated or returned draft.</summary>
    /// <returns>The updated revision, or a conflict when review cannot start.</returns>
    public Result<DocumentRevision> BeginReview() => Status switch
    {
        DocumentLifecycleStatus.DraftGenerated => Result.Ok(this with { Status = DocumentLifecycleStatus.InReview }),
        DocumentLifecycleStatus.ChangesRequested => ResetReview(Content, DocumentLifecycleStatus.InReview),
        _ => Result.Conflict<DocumentRevision>("This revision cannot enter human review."),
    };

    /// <summary>Changes an editable field and invalidates existing approvals when its value changes.</summary>
    /// <param name="fieldId">The stable field identifier.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The updated revision, or a failure when the field cannot be edited.</returns>
    /// <exception cref="ArgumentException">Thrown when the field identifier is blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public Result<DocumentRevision> Edit(string fieldId, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        ArgumentNullException.ThrowIfNull(value);
        if (Status is DocumentLifecycleStatus.Finalized or DocumentLifecycleStatus.Superseded or DocumentLifecycleStatus.Voided)
        {
            return Result.Conflict<DocumentRevision>("This revision is immutable.");
        }

        for (var index = 0; index < Content.Fields.Length; index++)
        {
            var field = Content.Fields[index];
            if (!string.Equals(field.Id, fieldId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!field.IsEditable)
            {
                return Result.Forbidden<DocumentRevision>("The document field is read-only.");
            }

            var phase = Status == DocumentLifecycleStatus.DraftGenerated ? Status : DocumentLifecycleStatus.InReview;
            return string.Equals(field.Value, value, StringComparison.Ordinal)
                ? Result.Ok(this)
                : ResetReview(Content.WithField(index, value), phase);
        }

        return Result.NotFound<DocumentRevision>("The document field does not exist.");
    }

    /// <summary>Records the decision of a human authorized by the host for the current lane.</summary>
    /// <param name="reviewed">The exact review version the human saw.</param>
    /// <param name="lane">The configured lane.</param>
    /// <param name="decision">Approval or a request for changes.</param>
    /// <param name="actorPseudonym">The host-supplied reviewer identifier.</param>
    /// <param name="occurredAt">The host's timestamp for the decision.</param>
    /// <param name="reasonCode">The host-defined review reason.</param>
    /// <param name="currentOrigin">The current source and template provenance supplied by the host.</param>
    /// <returns>The next revision snapshot, or a failure for stale content or an invalid review.</returns>
    public Result<DocumentRevision> Decide(
        ReviewRevision reviewed, string lane, ReviewDecision decision, string actorPseudonym,
        DateTimeOffset occurredAt, string reasonCode, DocumentOrigin currentOrigin)
    {
        if (Status != DocumentLifecycleStatus.InReview)
        {
            return Result.Conflict<DocumentRevision>("A decision requires active human review.");
        }

        var freshness = CheckOrigin(currentOrigin);
        if (freshness.IsFailure)
        {
            return freshness.ConvertError<DocumentRevision>();
        }

        return Review.RecordHumanDecision(reviewed, lane, decision, actorPseudonym, occurredAt, reasonCode)
            .Map(next => this with
            {
                Review = next,
                Status = next.IsApproved
                    ? DocumentLifecycleStatus.Approved
                    : next.ChangesRequested ? DocumentLifecycleStatus.ChangesRequested : DocumentLifecycleStatus.InReview,
            });
    }

    /// <summary>Seals the approved snapshot with an artifact durably stored by the host.</summary>
    /// <param name="artifactReference">The immutable handle returned by the host artifact store.</param>
    /// <param name="rendered">The reviewed version used to render the artifact.</param>
    /// <param name="currentOrigin">The source and template provenance at finalization.</param>
    /// <returns>The finalized revision, or a conflict when approval or provenance is stale.</returns>
    /// <exception cref="ArgumentException">Thrown when the artifact handle is blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the artifact handle is null.</exception>
    public Result<DocumentRevision> Seal(string artifactReference, ReviewRevision rendered, DocumentOrigin currentOrigin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactReference);
        var approval = CheckApproval(rendered);
        if (approval.IsFailure)
        {
            return approval.ConvertError<DocumentRevision>();
        }

        var freshness = CheckOrigin(currentOrigin);
        return freshness.IsFailure
            ? freshness.ConvertError<DocumentRevision>()
            : Result.Ok(this with { Status = DocumentLifecycleStatus.Finalized, ArtifactReference = artifactReference });
    }

    /// <summary>Withdraws a revision while retaining its evidence and artifact handle.</summary>
    /// <returns>The voided revision, or a conflict when it is already inactive.</returns>
    public Result<DocumentRevision> Void() => CanRegenerate
        ? Result.Ok(this with { Status = DocumentLifecycleStatus.Voided })
        : Result.Conflict<DocumentRevision>("This revision is already inactive.");

    /// <summary>Returns a diagnostic representation without document content or storage handles.</summary>
    /// <returns>The redacted representation.</returns>
    public override string ToString() => $"{nameof(DocumentRevision)} [redacted]";

    /// <summary>Creates a freshly generated document revision with its own empty review.</summary>
    internal static DocumentRevision Create(
        Guid lineageId, int number, DocumentOrigin origin, DocumentContent content, IEnumerable<string> lanes) =>
        new(lineageId, number, origin, content, ReviewHistory.Start(new ReviewRevision(1, content.CalculateHash(lineageId, number, origin)), lanes));

    /// <summary>Returns a historical snapshot and a fresh revision in the same lineage.</summary>
    /// <exception cref="OverflowException">Thrown when the next document revision number would overflow.</exception>
    internal (DocumentRevision Previous, DocumentRevision Current) Replace(DocumentOrigin origin, DocumentContent content) =>
        (this with { Status = DocumentLifecycleStatus.Superseded },
            Create(LineageId, checked(Number + 1), origin, content, Review.Lanes));

    private static bool HasConsistentState(DocumentRevisionSnapshot snapshot, ReviewHistory review)
    {
        var hasArtifact = snapshot.ArtifactReference is not null;
        return snapshot.Status switch
        {
            DocumentLifecycleStatus.DraftGenerated => !hasArtifact && review.Entries.IsEmpty,
            DocumentLifecycleStatus.InReview => !hasArtifact && !review.IsApproved && !review.ChangesRequested,
            DocumentLifecycleStatus.ChangesRequested => !hasArtifact && review.ChangesRequested,
            DocumentLifecycleStatus.Approved => !hasArtifact && review.IsApproved,
            DocumentLifecycleStatus.Finalized => hasArtifact && review.IsApproved,
            DocumentLifecycleStatus.Superseded or DocumentLifecycleStatus.Voided => !hasArtifact || review.IsApproved,
            _ => false,
        };
    }

    private static Result<DocumentRevision> InvalidSnapshot() =>
        Result.Invalid<DocumentRevision>("Stored document state is inconsistent.", "snapshot",
            "The stored content, review evidence and lifecycle state must agree.");

    private Result<DocumentRevision> ResetReview(DocumentContent content, DocumentLifecycleStatus status) =>
        Review.StartRevision(new ReviewRevision(checked(Review.Revision.Number + 1), content.CalculateHash(LineageId, Number, Origin)))
            .Map(review => this with { Review = review, Content = content, Status = status });

    /// <summary>Checks that the targeted review version can be finalized.</summary>
    internal Result CheckApproval(ReviewRevision reviewed) =>
        Status == DocumentLifecycleStatus.Approved && reviewed == Review.Revision
            ? Result.Ok()
            : Result.Conflict("Finalization requires an artifact rendered from the approved review version.");

    /// <summary>Checks that the source and template still match the draft provenance.</summary>
    internal Result CheckOrigin(DocumentOrigin currentOrigin)
    {
        if (currentOrigin is null || currentOrigin.SourceId != Origin.SourceId ||
            !string.Equals(currentOrigin.SourceVersion, Origin.SourceVersion, StringComparison.Ordinal))
        {
            return Result.Conflict("The document source changed after generation.");
        }

        return string.Equals(currentOrigin.TemplateId, Origin.TemplateId, StringComparison.Ordinal) &&
            string.Equals(currentOrigin.TemplateVersion, Origin.TemplateVersion, StringComparison.Ordinal)
            ? Result.Ok()
            : Result.Conflict("The document template changed after generation.");
    }
}
