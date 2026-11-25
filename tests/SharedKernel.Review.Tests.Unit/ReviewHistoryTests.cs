namespace SharedKernel.Review.Tests;

/// <summary>Exercises public review evidence contracts through a two-lane release review.</summary>
public sealed class ReviewHistoryTests
{
    private const string ContentA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ContentB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string TechnicalLane = "technical";
    private const string ApprovalLane = "approval";
    private const string Human = "reviewer-1";
    private const string Automation = "check-1";
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Human_approvals_complete_the_configured_lanes_in_order()
    {
        // Arrange
        var revision = new ReviewRevision(1, ContentA);
        string[] lanes = [TechnicalLane, ApprovalLane];
        var history = ReviewHistory.Start(revision, lanes);
        lanes[0] = "replacement";

        // Act
        var skipped = history.RecordHumanDecision(revision, ApprovalLane, ReviewDecision.Approved, Human, OccurredAt, "verified");
        var first = history.RecordHumanDecision(revision, TechnicalLane, ReviewDecision.Approved, Human, OccurredAt, "verified")
            .ShouldBeSuccess();
        var repeated = first.RecordHumanDecision(revision, TechnicalLane, ReviewDecision.Approved, Human, OccurredAt, "verified");
        var complete = first.RecordHumanDecision(revision, ApprovalLane, ReviewDecision.Approved, Human, OccurredAt, "verified")
            .ShouldBeSuccess();

        // Assert
        skipped.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        repeated.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        history.ExpectedHumanLane.ShouldBe(TechnicalLane);
        history.Entries.ShouldBeEmpty();
        first.ExpectedHumanLane.ShouldBe(ApprovalLane);
        first.IsApproved.ShouldBeFalse();
        complete.IsApproved.ShouldBeTrue();
        complete.ExpectedHumanLane.ShouldBeNull();
        complete.Entries.Select(entry => entry.Lane).ShouldBe([TechnicalLane, ApprovalLane]);
    }

    [Fact]
    public void Advisory_evidence_cannot_advance_a_human_lane_or_be_recorded_as_a_human_decision()
    {
        // Arrange
        var revision = new ReviewRevision(1, ContentA);
        var history = ReviewHistory.Start(revision, [TechnicalLane, ApprovalLane]);

        // Act
        var advisory = history.RecordAdvisory(revision, ApprovalLane, Automation, OccurredAt, "suggested-check").ShouldBeSuccess();
        var invalid = advisory.RecordHumanDecision(revision, TechnicalLane, ReviewDecision.Advisory, Human, OccurredAt, "invalid");

        // Assert
        advisory.ExpectedHumanLane.ShouldBe(TechnicalLane);
        advisory.IsApproved.ShouldBeFalse();
        invalid.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        var entry = advisory.Entries.ShouldHaveSingleItem();
        entry.Revision.ShouldBe(revision);
        entry.Decision.ShouldBe(ReviewDecision.Advisory);
        entry.ActorPseudonym.ShouldBe(Automation);
        entry.OccurredAt.ShouldBe(OccurredAt);
        entry.ReasonCode.ShouldBe("suggested-check");
    }

    [Fact]
    public void Editing_resets_approvals_and_preserves_the_old_revision_evidence()
    {
        // Arrange
        var firstRevision = new ReviewRevision(1, ContentA);
        var editedRevision = new ReviewRevision(2, ContentB);
        var revertedRevision = new ReviewRevision(3, ContentA);
        var history = ReviewHistory.Start(firstRevision, [TechnicalLane]);
        var approved = history.RecordHumanDecision(firstRevision, TechnicalLane, ReviewDecision.Approved, Human, OccurredAt, "verified")
            .ShouldBeSuccess();

        // Act
        var edited = approved.StartRevision(editedRevision).ShouldBeSuccess();
        var reverted = edited.StartRevision(revertedRevision).ShouldBeSuccess();
        var stale = reverted.RecordHumanDecision(firstRevision, TechnicalLane, ReviewDecision.Approved, Human, OccurredAt, "stale");
        var outOfOrder = reverted.StartRevision(editedRevision);

        // Assert
        approved.IsApproved.ShouldBeTrue();
        edited.IsApproved.ShouldBeFalse();
        reverted.IsApproved.ShouldBeFalse();
        reverted.ExpectedHumanLane.ShouldBe(TechnicalLane);
        reverted.Entries.ShouldBe(approved.Entries);
        reverted.Entries.ShouldHaveSingleItem().Revision.ShouldBe(firstRevision);
        stale.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        outOfOrder.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
    }

    [Fact]
    public void Requested_changes_require_a_new_revision_before_human_review_continues()
    {
        // Arrange
        var revision = new ReviewRevision(1, ContentA);
        var editedRevision = new ReviewRevision(2, ContentB);
        var history = ReviewHistory.Start(revision, [TechnicalLane, ApprovalLane]);
        var first = history.RecordHumanDecision(revision, TechnicalLane, ReviewDecision.Approved, Human, OccurredAt, "verified")
            .ShouldBeSuccess();

        // Act
        var rejected = first.RecordHumanDecision(revision, ApprovalLane, ReviewDecision.ChangesRequested, Human, OccurredAt, "revise")
            .ShouldBeSuccess();
        var rejectedApproval = rejected.RecordHumanDecision(revision, ApprovalLane, ReviewDecision.Approved, Human, OccurredAt, "premature");
        var edited = rejected.StartRevision(editedRevision).ShouldBeSuccess();

        // Assert
        rejected.ChangesRequested.ShouldBeTrue();
        rejected.IsApproved.ShouldBeFalse();
        rejected.ExpectedHumanLane.ShouldBeNull();
        rejectedApproval.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        edited.ChangesRequested.ShouldBeFalse();
        edited.ExpectedHumanLane.ShouldBe(TechnicalLane);
        edited.Entries.Select(entry => entry.Decision).ShouldBe([ReviewDecision.Approved, ReviewDecision.ChangesRequested]);
    }

    [Fact]
    public void Empty_and_oversized_reason_codes_are_rejected_without_evidence()
    {
        // Arrange
        var revision = new ReviewRevision(1, ContentA);
        var history = ReviewHistory.Start(revision, [TechnicalLane]);
        var atLimit = new string('x', ReviewHistory.MaximumReasonLength);
        var aboveLimit = atLimit + "x";
        Action missing = () => history.RecordAdvisory(revision, TechnicalLane, Automation, OccurredAt, " ");
        Action oversized = () => history.RecordHumanDecision(revision, TechnicalLane, ReviewDecision.Approved, Human, OccurredAt, aboveLimit);

        // Act
        var valid = history.RecordAdvisory(revision, TechnicalLane, Automation, OccurredAt, atLimit).ShouldBeSuccess();
        var missingFailure = missing.ShouldThrow<ArgumentException>();
        var oversizedFailure = oversized.ShouldThrow<ArgumentException>();

        // Assert
        valid.Entries.ShouldHaveSingleItem().ReasonCode.ShouldBe(atLimit);
        history.Entries.ShouldBeEmpty();
        missingFailure.ParamName.ShouldBe("reasonCode");
        oversizedFailure.ParamName.ShouldBe("reasonCode");
    }

    [Fact]
    public void Advisory_requires_a_configured_lane_and_the_exact_reviewed_content_hash()
    {
        // Arrange
        var revision = new ReviewRevision(1, ContentA);
        var wrongHash = new ReviewRevision(1, ContentB);
        var history = ReviewHistory.Start(revision, [TechnicalLane]);

        // Act
        var unknownLane = history.RecordAdvisory(revision, ApprovalLane, Automation, OccurredAt, "unknown");
        var stale = history.RecordAdvisory(wrongHash, TechnicalLane, Automation, OccurredAt, "stale");

        // Assert
        unknownLane.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        stale.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        history.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void New_review_requires_distinct_nonempty_lanes()
    {
        // Arrange
        var revision = new ReviewRevision(1, ContentA);
        Action noLanes = () => ReviewHistory.Start(revision, []);
        Action duplicateLanes = () => ReviewHistory.Start(revision, [TechnicalLane, TechnicalLane]);
        Action blankLane = () => ReviewHistory.Start(revision, [TechnicalLane, " "]);

        // Act
        var missingFailure = noLanes.ShouldThrow<ArgumentException>();
        var duplicateFailure = duplicateLanes.ShouldThrow<ArgumentException>();
        var blankFailure = blankLane.ShouldThrow<ArgumentException>();

        // Assert
        missingFailure.ParamName.ShouldBe("lanes");
        duplicateFailure.ParamName.ShouldBe("lanes");
        blankFailure.ParamName.ShouldBe("lanes");
    }

    [Fact]
    public void Revision_requires_a_positive_number_and_a_canonical_hexadecimal_hash()
    {
        // Arrange
        var uppercase = ContentA.ToUpperInvariant();
        var nonHex = new string('z', ContentA.Length);
        Action number = () => _ = new ReviewRevision(0, uppercase);
        Action length = () => _ = new ReviewRevision(1, "ab");
        Action invalid = () => _ = new ReviewRevision(1, nonHex);

        // Act
        var revision = new ReviewRevision(1, uppercase);
        var numberFailure = number.ShouldThrow<ArgumentOutOfRangeException>();
        var lengthFailure = length.ShouldThrow<ArgumentException>();
        var invalidFailure = invalid.ShouldThrow<ArgumentException>();

        // Assert
        revision.ShouldBe(new ReviewRevision(1, ContentA));
        revision.ContentHash.ShouldBe(uppercase);
        numberFailure.ParamName.ShouldBe("number");
        lengthFailure.ParamName.ShouldBe("contentHash");
        invalidFailure.ParamName.ShouldBe("contentHash");
    }
}
