namespace PandaBear.Review.Tests;

/// <summary>Verifies replay of persisted review evidence.</summary>
public sealed class ReviewHistorySnapshotTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Restoring_replays_current_lanes_and_keeps_evidence_from_an_earlier_review()
    {
        // Arrange
        var first = new ReviewRevision(1, Hash);
        var next = new ReviewRevision(3, Hash);
        var initial = ReviewHistory.Start(first, ["technical", "release"]);
        var partial = initial.RecordHumanDecision(first, "technical", ReviewDecision.Approved, "staff", Now, "checked").ShouldBeSuccess();
        var edited = partial.StartRevision(next).ShouldBeSuccess();
        var advised = edited.RecordAdvisory(next, "release", "checker", Now, "reported").ShouldBeSuccess();
        var current = advised.RecordHumanDecision(next, "technical", ReviewDecision.Approved, "staff", Now, "checked").ShouldBeSuccess();
        var snapshot = current.Snapshot();

        // Act
        var restored = ReviewHistory.Restore(snapshot).ShouldBeSuccess();

        // Assert
        restored.ExpectedHumanLane.ShouldBe("release");
        restored.IsApproved.ShouldBeFalse();
        restored.Revision.ShouldBe(next);
        restored.Snapshot().Evidence.AsEnumerable().ShouldBe(snapshot.Evidence);
    }

    [Fact]
    public void Rehydration_rejects_out_of_order_forged_and_malformed_evidence()
    {
        // Arrange
        var revision = new ReviewRevision(1, Hash);
        var history = ReviewHistory.Start(revision, ["technical", "release"]);
        var approved = history.RecordHumanDecision(revision, "technical", ReviewDecision.Approved, "staff", Now, "checked").ShouldBeSuccess();
        var snapshot = approved.Snapshot();
        var evidence = snapshot.Evidence[0];

        // Act
        var wrongLane = ReviewHistory.Restore(snapshot with { Evidence = [evidence with { Lane = "release" }] });
        var future = ReviewHistory.Restore(snapshot with { Evidence = [evidence with { Revision = new ReviewRevision(2, Hash) }] });
        var missingActor = ReviewHistory.Restore(snapshot with { Evidence = [evidence with { ActorPseudonym = " " }] });

        // Assert
        wrongLane.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        future.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        missingActor.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
    }

    [Fact]
    public void Restoring_a_revision_without_new_evidence_preserves_prior_approval_and_reopens_review()
    {
        // Arrange
        var first = new ReviewRevision(1, Hash);
        var next = new ReviewRevision(3, new string('b', Hash.Length));
        var initial = ReviewHistory.Start(first, ["technical"]);
        var approved = initial.RecordHumanDecision(first, "technical", ReviewDecision.Approved, "staff", Now, "checked").ShouldBeSuccess();
        var current = approved.StartRevision(next).ShouldBeSuccess();

        // Act
        var restored = ReviewHistory.Restore(current.Snapshot()).ShouldBeSuccess();

        // Assert
        restored.Revision.ShouldBe(next);
        restored.IsApproved.ShouldBeFalse();
        restored.ExpectedHumanLane.ShouldBe("technical");
        restored.Snapshot().Evidence.AsEnumerable().ShouldBe(approved.Snapshot().Evidence);
    }

    [Fact]
    public void Rehydration_rejects_a_replaced_digest_at_an_already_approved_revision_number()
    {
        // Arrange
        var revision = new ReviewRevision(1, Hash);
        var replacement = new ReviewRevision(revision.Number, new string('b', Hash.Length));
        var initial = ReviewHistory.Start(revision, ["technical"]);
        var approved = initial.RecordHumanDecision(revision, "technical", ReviewDecision.Approved, "staff", Now, "checked").ShouldBeSuccess();

        // Act
        var restored = ReviewHistory.Restore(approved.Snapshot() with { Revision = replacement });

        // Assert
        restored.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
    }

    [Fact]
    public void Rehydration_rejects_missing_snapshot_fields_and_evidence_entries()
    {
        // Arrange
        var revision = new ReviewRevision(1, Hash);
        var initial = ReviewHistory.Start(revision, ["technical"]);
        var approved = initial.RecordHumanDecision(revision, "technical", ReviewDecision.Approved, "staff", Now, "checked").ShouldBeSuccess();
        var snapshot = approved.Snapshot();
        (string Missing, ReviewHistorySnapshot? Snapshot)[] incomplete =
        [
            ("snapshot", null),
            ("revision", snapshot with { Revision = null! }),
            ("lanes", snapshot with { Lanes = [] }),
            ("evidence collection", snapshot with { Evidence = default }),
            ("first evidence entry", snapshot with { Evidence = [null!] }),
            ("later evidence entry", snapshot with { Evidence = [snapshot.Evidence[0], null!] }),
        ];

        // Act
        var restored = incomplete.Select(item => (item.Missing, Result: ReviewHistory.Restore(item.Snapshot))).ToArray();

        // Assert
        restored.ShouldAllSatisfy(item =>
            item.Result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, $"Missing {item.Missing} must invalidate the review."));
    }
}
