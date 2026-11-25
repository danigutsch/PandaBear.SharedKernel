using SharedKernel.DocumentWorkflow;
using SharedKernel.DocumentWorkflow.EntityFrameworkCore;
using SharedKernel.Results;
using SharedKernel.Review;
using static SharedKernel.EntityFrameworkCore.Tests.ReleaseDocumentFinalizationSetup;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Proves transaction, audit and durable replay for a neutral release document.</summary>
/// <param name="fixture">The shared PostgreSQL fixture.</param>
[Trait(TestTraitNames.CategoryName, IntegrationTestTraitValues.DatabaseIntegrationCategory)]
public sealed class PostgreSqlDocumentFinalizationCommandTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Replay_returns_the_original_receipt_after_the_revision_changes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var artifacts = new ReleaseDocumentArtifactStore();
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), artifacts, "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var command = ToRequest(approved);

        // Act
        var result = await scenario.Finalize(command, ct);
        var receipt = result.ShouldBeSuccess();
        var loaded = await scenario.Run(store => store.Load(approved.Revision.LineageId, approved.Revision.Number, ct));
        var finalized = loaded.ShouldBeSuccess();
        var voided = await scenario.Run(store => store.Save(finalized.Revision.Void().ShouldBeSuccess(), finalized.Version, ct));
        var replay = await scenario.Finalize(command, ct);
        var changedVersion = await scenario.Finalize(command with { ExpectedVersion = command.ExpectedVersion + 1 }, ct);
        var changedActor = await scenario.Finalize(command with { ActorPseudonym = "another-operator" }, ct);
        var audit = await scenario.ReadAudit(ct);

        // Assert
        receipt.ArtifactReference.ShouldBe("private/release-1");
        receipt.Version.ShouldBe(approved.Version + 1);
        finalized.Revision.Status.ShouldBe(DocumentLifecycleStatus.Finalized);
        voided.ShouldBeSuccess().Version.ShouldBe(receipt.Version + 1);
        replay.ShouldBeSuccess().ShouldBe(receipt);
        changedVersion.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        changedActor.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.Conflict);
        audit.ShouldHaveSingleItem().ShouldBe(new DocumentFinalizationAuditEntry(
            approved.Revision.LineageId, approved.Revision.Number, receipt.Version, Actor, RecordedAt.UtcDateTime));
        artifacts.Writes.ShouldBe(1);
        command.ToString().ShouldNotContain("private-client-token", StringComparison.Ordinal);
        receipt.ToString().ShouldNotContain(receipt.ArtifactReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stale_version_rolls_back_the_claim_and_allows_retry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var artifacts = new ReleaseDocumentArtifactStore();
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), artifacts, "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var command = ToRequest(approved);

        // Act
        var conflict = await scenario.Finalize(command with { ExpectedVersion = command.ExpectedVersion + 1 }, ct);
        var retry = await scenario.Finalize(command, ct);
        var audit = await scenario.ReadAudit(ct);

        // Assert
        conflict.ShouldBeFailure(ResultStatus.Conflict, ResultErrorCodes.ConcurrencyConflict);
        retry.ShouldBeSuccess().ArtifactReference.ShouldBe("private/release-1");
        audit.ShouldHaveSingleItem().Version.ShouldBe(approved.Version + 1);
        artifacts.Writes.ShouldBe(1);
    }

    [Theory]
    [InlineData("lineage")]
    [InlineData("revision")]
    [InlineData("version")]
    [InlineData("version-overflow")]
    [InlineData("review")]
    [InlineData("actor")]
    [InlineData("key")]
    public async Task An_incomplete_request_leaves_the_approved_revision_unchanged(string missing)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var artifacts = new ReleaseDocumentArtifactStore();
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), artifacts, "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var request = ToRequest(approved);
        var incomplete = missing switch
        {
            "lineage" => request with { LineageId = Guid.Empty },
            "revision" => request with { RevisionNumber = 0 },
            "version" => request with { ExpectedVersion = 0 },
            "version-overflow" => request with { ExpectedVersion = long.MaxValue },
            "review" => request with { Reviewed = null! },
            "actor" => request with { ActorPseudonym = " " },
            "key" => request with { IdempotencyKey = default },
            _ => throw new ArgumentOutOfRangeException(nameof(missing), missing, null),
        };

        // Act
        var rejected = await scenario.Finalize(incomplete, ct);
        var unchanged = await scenario.Run(store => store.Load(request.LineageId, request.RevisionNumber, ct));
        var audit = await scenario.ReadAudit(ct);

        // Assert
        rejected.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        unchanged.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.Approved);
        unchanged.ShouldBeSuccess().Version.ShouldBe(approved.Version);
        audit.ShouldBeEmpty();
        artifacts.Writes.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_missing_revision_or_mismatched_approval_rolls_back_the_claim(bool missingRevision)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var artifacts = new ReleaseDocumentArtifactStore();
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), artifacts, "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var request = ToRequest(approved);
        var invalid = missingRevision
            ? request with { RevisionNumber = request.RevisionNumber + 1 }
            : request with { Reviewed = new ReviewRevision(request.Reviewed.Number + 1, request.Reviewed.ContentHash) };
        var expectedStatus = missingRevision ? ResultStatus.NotFound : ResultStatus.Conflict;
        var expectedCode = missingRevision ? ResultErrorCodes.NotFound : ResultErrorCodes.Conflict;

        // Act
        var rejected = await scenario.Finalize(invalid, ct);
        var unchanged = await scenario.Run(store => store.Load(request.LineageId, request.RevisionNumber, ct));
        var auditBeforeRetry = await scenario.ReadAudit(ct);
        var retry = await scenario.Finalize(request, ct);
        var committedAudit = await scenario.ReadAudit(ct);

        // Assert
        rejected.ShouldBeFailure(expectedStatus, expectedCode);
        unchanged.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.Approved);
        unchanged.ShouldBeSuccess().Version.ShouldBe(approved.Version);
        auditBeforeRetry.ShouldBeEmpty();
        retry.ShouldBeSuccess().Version.ShouldBe(approved.Version + 1);
        committedAudit.ShouldHaveSingleItem().Version.ShouldBe(approved.Version + 1);
        artifacts.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task Replay_reports_a_corrupt_committed_revision_without_rerendering()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var artifacts = new ReleaseDocumentArtifactStore();
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), artifacts, "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var request = ToRequest(approved);
        var committed = await scenario.Finalize(request, ct);
        var receipt = committed.ShouldBeSuccess();
        await scenario.CorruptPayload(futureVersion: true, ct);

        // Act
        var replay = await scenario.Finalize(request, ct);
        var audit = await scenario.ReadAudit(ct);

        // Assert
        replay.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        audit.ShouldHaveSingleItem().Version.ShouldBe(receipt.Version);
        artifacts.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task An_audit_exception_rolls_back_saved_metadata_and_the_idempotency_claim()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), new ReleaseDocumentArtifactStore(), "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var command = ToRequest(approved);
        scenario.FailNextAudit();

        // Act
        Func<Task> send = () => scenario.Finalize(command, ct).AsTask();
        var failure = await send.ShouldThrow<InvalidOperationException>();
        var restored = await scenario.Run(store => store.Load(command.LineageId, command.RevisionNumber, ct));
        var rolledBackAudit = await scenario.ReadAudit(ct);
        var retry = await scenario.Finalize(command, ct);
        var replay = await scenario.Finalize(command, ct);
        var committedAudit = await scenario.ReadAudit(ct);

        // Assert
        failure.Message.ShouldBe("Simulated audit write failure.");
        restored.ShouldBeSuccess().Revision.Status.ShouldBe(DocumentLifecycleStatus.Approved);
        restored.ShouldBeSuccess().Version.ShouldBe(approved.Version);
        rolledBackAudit.ShouldBeEmpty();
        replay.ShouldBeSuccess().ShouldBe(retry.ShouldBeSuccess());
        committedAudit.ShouldHaveSingleItem().Version.ShouldBe(approved.Version + 1);
    }

    [Fact]
    public async Task Concurrent_delivery_only_renders_and_audits_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var artifacts = new ReleaseDocumentArtifactStore();
        var source = new ReleaseDocumentSource();
        var generator = new DocumentGenerator<string>(source, source);
        var finalizer = new DocumentFinalizer<string>(generator, new ReleaseDocumentRenderer(), artifacts, "text/plain", 1024);
        await using var scenario = await PostgreSqlDocumentWorkflowScenario.Create(fixture, ct, finalizer, RecordedAt);
        var approved = await PersistApproved(generator, scenario, ct);
        var command = ToRequest(approved);

        // Act
        var responses = await Task.WhenAll(scenario.Finalize(command, ct).AsTask(), scenario.Finalize(command, ct).AsTask());
        var audit = await scenario.ReadAudit(ct);

        // Assert
        responses[0].ShouldBeSuccess().ShouldBe(responses[1].ShouldBeSuccess());
        audit.ShouldHaveSingleItem().Version.ShouldBe(approved.Version + 1);
        artifacts.Writes.ShouldBe(1);
    }
}
