namespace SharedKernel.DocumentWorkflow.Tests;

/// <summary>Proves a neutral document workflow can round-trip persisted state safely.</summary>
public sealed class DocumentRevisionSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Superseded_revision_restores_its_review_and_original_artifact()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(host, host);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var review = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "staff", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var finalizer = new DocumentFinalizer<string>(generator, new HtmlDocumentRenderer(), host, "text/html", 16384);
        var sealing = await finalizer.FinalizeRevision(approved, approved.Review.Revision, ct);
        var finalized = sealing.ShouldBeSuccess();
        host.SourceVersion = "build-2";
        var replacement = await generator.Regenerate(finalized, ct);
        var replaced = replacement.ShouldBeSuccess();
        var snapshot = replaced.Previous.Snapshot();
        var content = snapshot.Content;
        var copiedContent = new DocumentContent(content.Language, content.Title, content.SectionHeading,
            [.. content.Fields], content.Branding);

        // Act
        var restored = DocumentRevision.Restore(snapshot with { Content = copiedContent }).ShouldBeSuccess();

        // Assert
        restored.Status.ShouldBe(DocumentLifecycleStatus.Superseded);
        restored.LineageId.ShouldBe(finalized.LineageId);
        restored.Review.IsApproved.ShouldBeTrue();
        restored.Review.Entries.ShouldHaveSingleItem().ActorPseudonym.ShouldBe("staff");
        restored.ArtifactReference.ShouldBe(finalized.ArtifactReference);
        host.ReadArtifact(restored.ArtifactReference!).ShouldContain("Ready for review", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forged_lifecycle_state_changed_content_and_unapproved_artifacts_are_rejected()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(host, host);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var snapshot = draft.Snapshot();
        var edited = draft.Edit("summary", "Different value").ShouldBeSuccess();

        // Act
        var status = DocumentRevision.Restore(snapshot with { Status = DocumentLifecycleStatus.Approved });
        var unknownStatus = DocumentRevision.Restore(snapshot with { Status = (DocumentLifecycleStatus)int.MaxValue });
        var content = DocumentRevision.Restore(snapshot with { Content = edited.Content });
        var artifact = DocumentRevision.Restore(snapshot with { Status = DocumentLifecycleStatus.Voided, ArtifactReference = "private/forged" });

        // Assert
        status.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        unknownStatus.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        content.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        artifact.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
    }

    [Fact]
    public async Task Incomplete_persisted_identity_provenance_content_and_review_are_rejected()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(host, host);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["release"], ct);
        var snapshot = generated.ShouldBeSuccess().Snapshot();
        (string Missing, DocumentRevisionSnapshot? Snapshot)[] incomplete =
        [
            ("snapshot", null),
            ("lineage", snapshot with { LineageId = Guid.Empty }),
            ("revision number", snapshot with { Number = 0 }),
            ("provenance", snapshot with { Origin = null! }),
            ("source identity", snapshot with { Origin = snapshot.Origin with { SourceId = Guid.Empty } }),
            ("source version", snapshot with { Origin = snapshot.Origin with { SourceVersion = " " } }),
            ("template identity", snapshot with { Origin = snapshot.Origin with { TemplateId = " " } }),
            ("template version", snapshot with { Origin = snapshot.Origin with { TemplateVersion = " " } }),
            ("content", snapshot with { Content = null! }),
            ("artifact handle", snapshot with { ArtifactReference = " " }),
            ("review", snapshot with { Review = null! }),
        ];

        // Act
        var restored = incomplete.Select(item => (item.Missing, Result: DocumentRevision.Restore(item.Snapshot))).ToArray();

        // Assert
        restored.ShouldAllSatisfy(item =>
            item.Result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, $"Missing {item.Missing} must invalidate the revision."));
    }

    [Fact]
    public async Task Partial_and_returned_reviews_restore_their_lifecycle_and_decisions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var host = new ReleaseNoteConsumer();
        var generator = new DocumentGenerator<string>(host, host);
        var generated = await generator.Generate(Guid.CreateVersion7(), Guid.CreateVersion7(), ["technical", "release"], ct);
        var reviewing = generated.ShouldBeSuccess().BeginReview().ShouldBeSuccess();
        var partial = reviewing.Decide(reviewing.Review.Revision, "technical", ReviewDecision.Approved,
            "reviewer", Now, "checked", reviewing.Origin).ShouldBeSuccess();
        var returned = partial.Decide(partial.Review.Revision, "release", ReviewDecision.ChangesRequested,
            "editor", Now, "revise", partial.Origin).ShouldBeSuccess();

        // Act
        var restoredPartial = DocumentRevision.Restore(partial.Snapshot()).ShouldBeSuccess();
        var restoredReturned = DocumentRevision.Restore(returned.Snapshot()).ShouldBeSuccess();

        // Assert
        restoredPartial.Status.ShouldBe(DocumentLifecycleStatus.InReview);
        restoredPartial.Review.ExpectedHumanLane.ShouldBe("release");
        restoredPartial.Review.Snapshot().Evidence.AsEnumerable().ShouldBe(partial.Review.Snapshot().Evidence);
        restoredReturned.Status.ShouldBe(DocumentLifecycleStatus.ChangesRequested);
        restoredReturned.Review.ExpectedHumanLane.ShouldBeNull();
        restoredReturned.Review.Snapshot().Evidence.AsEnumerable().ShouldBe(returned.Review.Snapshot().Evidence);
    }
}
