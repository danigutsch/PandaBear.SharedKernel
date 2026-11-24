using PandaBear.DocumentWorkflow;
using PandaBear.Results;
using PandaBear.Review;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Proves document workflow persistence against PostgreSQL.</summary>
/// <param name="fixture">The shared PostgreSQL test fixture.</param>
[Trait(TestTraitNames.CategoryName, IntegrationTestTraitValues.DatabaseIntegrationCategory)]
public sealed class PostgreSqlDocumentWorkflowTests(PostgreSqlFixture fixture)
{
    private static readonly Guid Lineage = new("d77b34e8-4449-4f64-991c-aa36a28cdd65");
    private static readonly Guid Source = new("143b484f-7510-4c34-b112-7e827a2508f0");
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Persists_restores_and_rejects_a_stale_revision_write()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var created = await database.Run(store => store.Create(draft, ct));
        var initial = created.ShouldBeSuccess();
        var review = draft.BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "staff", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var sealedRevision = approved.Seal("private/release-1", approved.Review.Revision, approved.Origin).ShouldBeSuccess();

        // Act
        var saved = await database.Run(store => store.Save(sealedRevision, initial.Version, ct));
        var stale = await database.Run(store => store.Save(review, initial.Version, ct));
        var loaded = await database.Run(store => store.Load(Lineage, 1, ct));

        // Assert
        saved.ShouldBeSuccess().Version.ShouldBe(initial.Version + 1);
        stale.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.ConcurrencyConflict);
        var restored = loaded.ShouldBeSuccess().Revision;
        restored.Status.ShouldBe(DocumentLifecycleStatus.Finalized);
        restored.ArtifactReference.ShouldBe("private/release-1");
        restored.Content.Branding.ShouldNotBeNull().BrandName.ShouldBe("Release office");
        restored.Review.Entries.ShouldHaveSingleItem().ActorPseudonym.ShouldBe("staff");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rejects_invalid_payloads_after_committed_regeneration(bool futureVersion)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var created = await database.Run(store => store.Create(draft, ct));
        var current = created.ShouldBeSuccess();
        var replacement = await generator.Regenerate(draft, ct);
        var (previous, next) = replacement.ShouldBeSuccess();

        // Act
        var saved = await database.Run(store => store.Replace(previous, next, current.Version, ct));
        var history = await database.Run(store => store.Load(Lineage, 1, ct));
        await database.CorruptPayload(futureVersion, ct);
        var invalid = await database.Run(store => store.Load(Lineage, 2, ct));
        var blockedWrite = await database.Run(store => store.Save(next, saved.ShouldBeSuccess().Version, ct));

        // Assert
        saved.ShouldBeSuccess().Revision.Number.ShouldBe(2);
        history.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.Superseded);
        invalid.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        blockedWrite.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
    }

    [Fact]
    public async Task A_lineage_can_only_start_with_its_first_generated_draft()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var review = draft.BeginReview().ShouldBeSuccess();
        var replacement = await generator.Regenerate(draft, ct);
        var (_, next) = replacement.ShouldBeSuccess();

        // Act
        var reviewCreate = await database.Run(store => store.Create(review, ct));
        var successorCreate = await database.Run(store => store.Create(next, ct));
        var storedDraft = await database.Run(store => store.Create(draft, ct));
        var absentSuccessor = await database.Run(store => store.Load(Lineage, 2, ct));

        // Assert
        reviewCreate.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        successorCreate.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        storedDraft.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.DraftGenerated);
        absentSuccessor.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);
    }

    [Fact]
    public async Task Supersession_requires_an_atomic_replacement_of_the_current_version()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var created = await database.Run(store => store.Create(draft, ct));
        var version = created.ShouldBeSuccess().Version;
        var replacement = await generator.Regenerate(draft, ct);
        var (historical, successor) = replacement.ShouldBeSuccess();

        // Act
        var directSave = await database.Run(store => store.Save(historical, version, ct));
        var staleReplace = await database.Run(store => store.Replace(historical, successor, version + 1, ct));
        var retained = await database.Run(store => store.Load(Lineage, 1, ct));
        var absentSuccessor = await database.Run(store => store.Load(Lineage, 2, ct));

        // Assert
        directSave.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        staleReplace.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.ConcurrencyConflict);
        retained.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.DraftGenerated);
        retained.ShouldBeSuccess().Version.ShouldBe(version);
        absentSuccessor.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);
    }

    [Fact]
    public async Task Replacement_preserves_lineage_sequence_lifecycle_and_source()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var created = await database.Run(store => store.Create(draft, ct));
        var version = created.ShouldBeSuccess().Version;
        var replacement = await generator.Regenerate(draft, ct);
        var (historical, successor) = replacement.ShouldBeSuccess();
        var inReview = successor.BeginReview().ShouldBeSuccess();
        var otherLineageDraft = await generator.Generate(Guid.NewGuid(), Source, ["release"], ct);
        var otherLineagePair = await generator.Regenerate(otherLineageDraft.ShouldBeSuccess(), ct);
        var (_, otherLineageSuccessor) = otherLineagePair.ShouldBeSuccess();
        var otherSourceDraft = await generator.Generate(Lineage, Guid.NewGuid(), ["release"], ct);
        var otherSourcePair = await generator.Regenerate(otherSourceDraft.ShouldBeSuccess(), ct);
        var (_, otherSourceSuccessor) = otherSourcePair.ShouldBeSuccess();

        // Act
        var differentLineage = await database.Run(store => store.Replace(historical, otherLineageSuccessor, version, ct));
        var repeatedNumber = await database.Run(store => store.Replace(historical, draft, version, ct));
        var activePrevious = await database.Run(store => store.Replace(draft, successor, version, ct));
        var reviewSuccessor = await database.Run(store => store.Replace(historical, inReview, version, ct));
        var differentSource = await database.Run(store => store.Replace(historical, otherSourceSuccessor, version, ct));
        var retained = await database.Run(store => store.Load(Lineage, 1, ct));
        var absentSuccessor = await database.Run(store => store.Load(Lineage, 2, ct));

        // Assert
        differentLineage.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        repeatedNumber.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        activePrevious.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        reviewSuccessor.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        differentSource.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        retained.ShouldBeSuccess().Revision.Origin.SourceId.ShouldBe(Source);
        retained.ShouldBeSuccess().Version.ShouldBe(version);
        absentSuccessor.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);
    }

    [Fact]
    public async Task A_concurrent_committed_writer_causes_a_typed_database_conflict()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var initial = await database.Run(store => store.Create(draft, ct));
        var version = initial.ShouldBeSuccess().Version;
        var first = draft.Edit("summary", "first writer").ShouldBeSuccess();
        var second = draft.Edit("summary", "second writer").ShouldBeSuccess();

        // Act
        var race = await database.Race(first, second, version, ct);
        var loaded = await database.Run(store => store.Load(Lineage, 1, ct));

        // Assert
        race.Lost.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.ConcurrencyConflict);
        race.Won.ShouldBeSuccess().Version.ShouldBe(version + 1);
        loaded.ShouldBeSuccess().Revision.Content.Fields.ShouldHaveSingleItem().Value.ShouldBe("second writer");
    }

    [Fact]
    public async Task A_duplicate_successor_rolls_back_the_historical_update()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var initial = await database.Run(store => store.Create(draft, ct));
        var version = initial.ShouldBeSuccess().Version;
        var replacement = await generator.Regenerate(draft, ct);
        var (previous, next) = replacement.ShouldBeSuccess();
        await database.BlockReplacement(ct);

        // Act
        var duplicate = await database.Run(store => store.Replace(previous, next, version, ct));
        var restored = await database.Run(store => store.Load(Lineage, 1, ct));

        // Assert
        duplicate.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.ConcurrencyConflict);
        restored.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.DraftGenerated);
        restored.ShouldBeSuccess().Version.ShouldBe(version);
    }

    [Fact]
    public async Task A_current_storage_version_cannot_be_used_to_erase_committed_review_evidence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct);
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var generated = await generator.Generate(Lineage, Source, ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var created = await database.Run(store => store.Create(draft, ct));
        var review = draft.BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, "staff", Now, "checked", review.Origin)
            .ShouldBeSuccess();
        var persisted = await database.Run(store => store.Save(approved, created.ShouldBeSuccess().Version, ct));
        var current = persisted.ShouldBeSuccess();

        // Act
        var truncated = await database.Run(store => store.Save(review, current.Version, ct));
        var restored = await database.Run(store => store.Load(Lineage, 1, ct));

        // Assert
        truncated.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        restored.ShouldBeSuccess().Revision.Review.Entries.ShouldHaveSingleItem().ActorPseudonym.ShouldBe("staff");
    }
}
