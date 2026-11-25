using System.Text;

namespace SharedKernel.DocumentWorkflow.Tests;

/// <summary>Exercises the lifecycle for a release-note consumer.</summary>
public sealed class DocumentWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Neutral_consumer_reviews_renders_and_revises_without_replacing_the_old_artifact()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var consumer = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(consumer, consumer);
        var draftResult = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["technical", "release"], ct);
        var draft = draftResult.ShouldBeSuccess();
        var reviewing = draft.BeginReview().ShouldBeSuccess();
        var technical = reviewing.Decide(reviewing.Review.Revision, "technical", ReviewDecision.Approved, "person-1", Now, "checked", draft.Origin)
            .ShouldBeSuccess();
        var approved = technical.Decide(technical.Review.Revision, "release", ReviewDecision.Approved, "person-2", Now, "checked", draft.Origin)
            .ShouldBeSuccess();

        // Act
        var renderRequest = approved.Content.ToRenderRequest();
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(renderRequest));
        consumer.StoreArtifact("private/artifacts/release-1", html);
        var origin = await generator.ReadCurrentOrigin(approved.Origin.SourceId, ct);
        var sealedRevision = approved.Seal("private/artifacts/release-1", approved.Review.Revision, origin.ShouldBeSuccess()).ShouldBeSuccess();
        consumer.SourceVersion = "build-2";
        consumer.BuildNumber = "20";
        var regeneration = await generator.Regenerate(sealedRevision, ct);
        var replacement = regeneration.ShouldBeSuccess();

        // Assert
        html.ShouldContain("<h2>Build results</h2>", StringComparison.Ordinal);
        renderRequest.Title.ShouldBe("Release note");
        renderRequest.Branding.ShouldNotBeNull().BrandName.ShouldBe("Release office");
        renderRequest.Sections.ShouldHaveSingleItem().Fields.Select(field => field.Value).ShouldBe(["19", "Ready for review"]);
        sealedRevision.Status.ShouldBe(DocumentLifecycleStatus.Finalized);
        sealedRevision.Edit("summary", "late edit").ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        replacement.Previous.Status.ShouldBe(DocumentLifecycleStatus.Superseded);
        replacement.Previous.ArtifactReference.ShouldBe("private/artifacts/release-1");
        consumer.ReadArtifact(replacement.Previous.ArtifactReference!).ShouldBe(html);
        replacement.Current.Content.Fields.Select(field => field.Value).ShouldBe(["20", "Ready for review"]);
        replacement.Current.Number.ShouldBe(2);
        replacement.Current.LineageId.ShouldBe(draft.LineageId);
        replacement.Current.Origin.SourceVersion.ShouldBe("build-2");
        replacement.Current.Status.ShouldBe(DocumentLifecycleStatus.DraftGenerated);
    }

    [Fact]
    public async Task Edit_revokes_review_and_immutable_fields_and_stale_source_are_rejected()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var consumer = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(consumer, consumer);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["technical"], ct);
        var draft = generated.ShouldBeSuccess();
        var reviewing = draft.BeginReview().ShouldBeSuccess();
        var approved = reviewing.Decide(reviewing.Review.Revision, "technical", ReviewDecision.Approved, "person-1", Now, "checked", draft.Origin)
            .ShouldBeSuccess();

        // Act
        var locked = approved.Edit("build-number", "20");
        var edited = approved.Edit("summary", "Edited after approval").ShouldBeSuccess();
        var staleReview = edited.Decide(approved.Review.Revision, "technical", ReviewDecision.Approved, "person-1", Now, "checked", draft.Origin);
        var unapproved = edited.Seal("private/artifacts/release-1", edited.Review.Revision, draft.Origin);
        consumer.SourceVersion = "build-2";
        var currentSource = await generator.ReadCurrentOrigin(draft.Origin.SourceId, ct);
        var staleOrigin = approved.Seal("private/artifacts/release-1", approved.Review.Revision, currentSource.ShouldBeSuccess());
        consumer.SourceVersion = "build-1";
        consumer.TemplateVersion = "2";
        var currentTemplate = await generator.ReadCurrentOrigin(draft.Origin.SourceId, ct);
        var staleTemplate = approved.Seal("private/artifacts/release-1", approved.Review.Revision, currentTemplate.ShouldBeSuccess());

        // Assert
        locked.ShouldBeFailure(ResultStatus.Forbidden, ResultErrorCodes.Forbidden);
        edited.Status.ShouldBe(DocumentLifecycleStatus.InReview);
        edited.Review.IsApproved.ShouldBeFalse();
        edited.Review.Entries.ShouldBe(approved.Review.Entries);
        edited.Review.Revision.ContentHash.ShouldNotBe(approved.Review.Revision.ContentHash);
        staleReview.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        unapproved.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        staleOrigin.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        staleTemplate.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
    }

    [Fact]
    public async Task Identical_replacement_content_rejects_a_review_decision_from_the_prior_revision()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var consumer = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(consumer, consumer);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["technical"], ct);
        var previous = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        consumer.TemplateVersion = "2";
        var regenerated = await generator.Regenerate(previous, ct);
        var next = regenerated.ShouldBeSuccess().Current.BeginReview().ShouldBeSuccess();

        // Act
        var stale = next.Decide(previous.Review.Revision, "technical", ReviewDecision.Approved, "person-1", Now, "checked", next.Origin);

        // Assert
        stale.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        next.Review.Revision.ContentHash.ShouldNotBe(previous.Review.Revision.ContentHash);
    }

    [Fact]
    public async Task Changes_requested_requires_fresh_review_and_voided_revisions_cannot_be_replaced()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var consumer = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(consumer, consumer);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["technical"], ct);
        var reviewing = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var returned = reviewing.Decide(reviewing.Review.Revision, "technical", ReviewDecision.ChangesRequested,
            "person-1", Now, "revise", reviewing.Origin).ShouldBeSuccess();

        // Act
        var reopened = returned.BeginReview().ShouldBeSuccess();
        var voided = reopened.Void().ShouldBeSuccess();
        var replacement = await generator.Regenerate(voided, ct);

        // Assert
        returned.Status.ShouldBe(DocumentLifecycleStatus.ChangesRequested);
        reopened.Status.ShouldBe(DocumentLifecycleStatus.InReview);
        reopened.Review.Revision.Number.ShouldBe(reviewing.Review.Revision.Number + 1);
        reopened.Review.Entries.ShouldBe(returned.Review.Entries);
        voided.Status.ShouldBe(DocumentLifecycleStatus.Voided);
        replacement.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
    }
}
