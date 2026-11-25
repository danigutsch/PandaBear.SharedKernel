using System.Text;

namespace PandaBear.DocumentWorkflow.Tests;

/// <summary>Verifies artifact storage using the release-note consumer.</summary>
public sealed class DocumentFinalizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private const int ArtifactLimit = 16_384;

    [Fact]
    public async Task Approved_finalization_is_repeatable_and_stores_exact_renderer_output_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(host, host);
        var render = new HtmlDocumentRenderer();
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var review = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "reviewer", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var bytes = render.Render(approved.Content.ToRenderRequest());
        var finalizer = new DocumentFinalizer<string>(generator, render, host, "text/html", bytes.Length);

        // Act
        var first = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);
        var retried = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);

        // Assert
        var revision = first.ShouldBeSuccess();
        revision.Status.ShouldBe(DocumentLifecycleStatus.Finalized);
        retried.ShouldBeSuccess().ArtifactReference.ShouldBe(revision.ArtifactReference);
        host.ArtifactCount.ShouldBe(1);
        host.ReadArtifact(revision.ArtifactReference!).ShouldBe(Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Unapproved_stale_and_oversized_artifacts_are_rejected_before_storage()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(host, host);
        var render = new HtmlDocumentRenderer();
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var review = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "reviewer", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var finalizer = new DocumentFinalizer<string>(generator, render, host, "text/html", ArtifactLimit);
        var limited = new DocumentFinalizer<string>(generator, render, host, "text/html", 1);

        // Act
        var unapproved = await finalizer.FinalizeRevision(review, review.Review.Revision, ct);
        var oversized = await limited.FinalizeRevision(approved, approved.Review.Revision, ct);
        host.SourceVersion = "build-2";
        var stale = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);

        // Assert
        unapproved.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        oversized.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        stale.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        host.ArtifactCount.ShouldBe(0);
    }

    [Fact]
    public async Task Source_change_during_storage_keeps_the_revision_unsealed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer { ChangeSourceAfterArtifactWrite = true };
        var generator = new DocumentGenerator<string>(host, host);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var review = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "reviewer", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var finalizer = new DocumentFinalizer<string>(generator, new HtmlDocumentRenderer(), host, "text/html", ArtifactLimit);

        // Act
        var result = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);

        // Assert
        result.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        approved.Status.ShouldBe(DocumentLifecycleStatus.Approved);
        approved.ArtifactReference.ShouldBeNull();
        host.ArtifactCount.ShouldBe(1);
    }

    [Fact]
    public async Task Store_failure_keeps_the_revision_available_for_retry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer { ArtifactStoreUnavailable = true };
        var generator = new DocumentGenerator<string>(host, host);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var review = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "reviewer", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var finalizer = new DocumentFinalizer<string>(generator, new HtmlDocumentRenderer(), host, "text/html", ArtifactLimit);

        // Act
        var failed = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);
        host.ArtifactStoreUnavailable = false;
        var retried = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);

        // Assert
        failed.ShouldBeFailure(ResultStatus.Unavailable, ResultErrorCodes.Unavailable);
        approved.ArtifactReference.ShouldBeNull();
        retried.ShouldBeSuccess().Status.ShouldBe(DocumentLifecycleStatus.Finalized);
        host.ArtifactCount.ShouldBe(1);
    }
}
