using SharedKernel.Testing;
using SharedKernel.Vault.Merge;
using SharedKernel.Vault.Pipeline;
using SharedKernel.Vault.Review;

namespace SharedKernel.Vault.Tests.Review;

/// <summary>Exercises resumable review decisions directly through the Vault library.</summary>
public sealed class ReviewCheckpointTests
{
    [Fact]
    public void A_checkpoint_keeps_pending_choices_but_does_not_restore_revealed_values()
    {
        // Arrange
        var scanned = ReviewCheckpointFixtures.ConflictingLogins();
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var session = new ReviewSession(pipeline, scanned, MergeDecisions.None);
        string groupKey = session.Groups(GroupFilter.All, null).ShouldHaveSingleItem().Key;
        _ = session.ToggleReveal(groupKey, FieldPaths.LoginPassword);
        MergeDecisions checkpoint = MergeDecisions.None;

        // Act
        session.Save(snapshot => checkpoint = snapshot);
        var restored = new ReviewSession(pipeline, scanned, checkpoint);
        var field = restored.Fields(groupKey).ShouldHaveSingleItem(field => field.FieldPath == FieldPaths.LoginPassword);

        // Assert
        checkpoint.Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem().Kind.ShouldBe(FieldChoiceKind.Pending);
        restored.Pending.Fields.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
        field.IsRevealed.ShouldBeFalse();
        field.Values.ShouldBe([ReviewText.Mask, ReviewText.Mask]);
        DecisionsFile.Serialize(checkpoint).ShouldNotContain(ReviewCheckpointFixtures.KeeperPassword, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_checkpoint_stays_dirty_and_can_be_retried()
    {
        // Arrange
        var scanned = ReviewCheckpointFixtures.ConflictingLogins();
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var session = new ReviewSession(pipeline, scanned, MergeDecisions.None);
        string groupKey = session.Groups(GroupFilter.All, null).ShouldHaveSingleItem().Key;
        session.ChooseMember(groupKey, FieldPaths.LoginPassword, ReviewCheckpointFixtures.KeeperKey);
        Action save = () => session.Save(static _ => throw new IOException("Fixture save failed."));
        MergeDecisions checkpoint = MergeDecisions.None;

        // Act
        save.ShouldThrow<IOException>();
        bool dirtyAfterFailure = session.IsDirty;
        bool savedAfterFailure = session.Saved;
        session.Save(snapshot => checkpoint = snapshot);
        var restored = new ReviewSession(pipeline, scanned, checkpoint);

        // Assert
        dirtyAfterFailure.ShouldBeTrue();
        savedAfterFailure.ShouldBeFalse();
        session.IsDirty.ShouldBeFalse();
        session.Saved.ShouldBeTrue();
        restored.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, ReviewCheckpointFixtures.KeeperKey));
        restored.Pending.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void An_unresolved_review_cannot_write_through_the_library()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var session = new ReviewSession(pipeline, ReviewCheckpointFixtures.ConflictingLogins(), MergeDecisions.None);
        string path = Path.Combine(directory.Path, "merged.json");
        var output = new PipelineOutput(path, Plaintext: true);
        Action export = () => session.Write(output, pipeline);

        // Act
        export.ShouldThrow<InvalidOperationException>();

        // Assert
        session.Written.ShouldBeFalse();
        File.Exists(path).ShouldBeFalse();
    }
}
