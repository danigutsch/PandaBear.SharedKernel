using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PandaBear.Idempotency;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Exercises completed integration event inbox observations in PostgreSQL.</summary>
[Trait(SharedKernelTestTraitNames.CategoryName, IntegrationTestTraitValues.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraitValues.IntegrationEventTransport)]
public sealed class PostgreSqlInboxDuplicateTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private const string Consumer = "gallery";
    private const string ConsumerScope = "gallery.integration-event.v1";

    [Fact]
    public async Task Completed_duplicate_is_counted_without_restarting_the_inbox_entry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        await scenario.Complete(operation, CompletedAt, ct, owner: "retention-owner");
        var legacy = await scenario.ReadHealth(Consumer, ct);
        var before = await scenario.ReadEntry(operation, ct);
        var parentVersion = await scenario.ReadEntryVersion(operation, ct);
        var observedAt = CompletedAt.AddMinutes(1);

        // Act
        var skipped = await scenario.TryStart(operation, observedAt, ct);
        var recorded = await scenario.Record(operation.Scope, eventId, observedAt, ct);
        var page = await scenario.ReadPage(Consumer, ct, eventId);
        var health = await scenario.ReadHealth(Consumer, ct);
        var after = await scenario.ReadEntry(operation, ct);
        var resultingVersion = await scenario.ReadEntryVersion(operation, ct);

        // Assert
        skipped.Started.ShouldBeFalse();
        skipped.ExistingEntry.ShouldNotBeNull().CompletedAt.ShouldBe(CompletedAt);
        recorded.ShouldBeTrue();
        legacy.ShouldBe(default);
        page.Rows.ShouldHaveSingleItem().ShouldBe(new IntegrationEventInboxDuplicateRow(eventId, 1, observedAt));
        health.ShouldBe(new IntegrationEventInboxDuplicateHealth(1, 1, observedAt));
        after.ShouldBe(before);
        resultingVersion.ShouldBe(parentVersion, "the completed parent row is never rewritten during observation");
    }

    [Fact]
    public async Task Concurrent_observations_preserve_every_count_and_the_latest_time()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        var latest = CompletedAt.AddMinutes(25);
        await scenario.Complete(operation, CompletedAt, ct);

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(attempt =>
            scenario.Record(operation.Scope, eventId, latest.AddMinutes(-attempt).ToOffset(TimeSpan.FromHours(2)), ct)));
        var page = await scenario.ReadPage(Consumer, ct);

        // Assert
        results.ShouldBe(Enumerable.Repeat(true, 16));
        page.Rows.ShouldHaveSingleItem().ShouldBe(new IntegrationEventInboxDuplicateRow(eventId, 16, latest));
    }

    [Fact]
    public async Task Busy_missing_and_recovered_initial_deliveries_do_not_count_as_duplicates()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        await scenario.TryStart(operation, CompletedAt, ct);

        // Act
        var busy = await scenario.TryStart(operation, CompletedAt.AddMinutes(1), ct);
        var busyRecorded = await scenario.Record(operation.Scope, eventId, CompletedAt.AddMinutes(1), ct);
        var absentRecorded = await scenario.Record(operation.Scope, Guid.CreateVersion7(), CompletedAt.AddMinutes(1), ct);
        var recovered = await scenario.TryStart(operation, CompletedAt.AddMinutes(6), ct);
        var recoveredRecorded = await scenario.Record(operation.Scope, eventId, CompletedAt.AddMinutes(6), ct);
        var health = await scenario.ReadHealth(Consumer, ct);

        // Assert
        busy.ExistingEntry.ShouldNotBeNull().State.ShouldBe(IdempotencyEntryState.Started);
        recovered.Started.ShouldBeTrue();
        busyRecorded.ShouldBeFalse();
        absentRecorded.ShouldBeFalse();
        recoveredRecorded.ShouldBeFalse();
        health.ShouldBe(default);
    }

    [Fact]
    public async Task A_consumer_receives_only_its_bounded_event_metadata()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        foreach (var eventId in eventIds)
        {
            var owned = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
            await scenario.Complete(owned, CompletedAt, ct);
            await scenario.Record(owned.Scope, eventId, CompletedAt, ct);
        }

        const string otherConsumer = "media";
        var other = PostgreSqlInboxDuplicateScenario.Operation("media.integration-event.v1", eventIds[0]);
        await scenario.Complete(other, CompletedAt, ct);
        await scenario.Record(other.Scope, eventIds[0], CompletedAt.AddMinutes(1), ct);

        // Act
        scenario.Commands.Clear();
        var first = await scenario.ReadPage(Consumer, ct, limit: 2);
        var pageSql = scenario.Commands.ShouldHaveSingleItem();
        var second = await scenario.ReadPage(Consumer, ct, limit: 2, cursor: first.Next);
        var otherEvents = await scenario.ReadPage(otherConsumer, ct, eventIds[0]);
        var missing = await scenario.ReadPage(otherConsumer, ct, eventIds[1]);

        // Assert
        first.Rows.Count.ShouldBe(2);
        first.Next.ShouldNotBeNull();
        second.Rows.ShouldHaveSingleItem();
        second.Next.ShouldBeNull();
        first.Rows.Concat(second.Rows).Select(row => row.EventId).ShouldBe(eventIds.OrderDescending());
        otherEvents.Rows.ShouldHaveSingleItem().LastObservedAt.ShouldBe(CompletedAt.AddMinutes(1));
        missing.Rows.ShouldBeEmpty();
        pageSql.ShouldContain("LIMIT", StringComparison.Ordinal);
        pageSql.ShouldNotContain("\"Scope\"", StringComparison.Ordinal);
        pageSql.ShouldNotContain("\"Key\"", StringComparison.Ordinal);
        pageSql.ShouldNotContain("Fingerprint", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Observation_cutoff_is_inclusive_and_applies_before_the_continuation_limit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var boundary = CompletedAt.AddMinutes(2);
        var cutoff = boundary.ToOffset(TimeSpan.FromHours(3));
        var recent = Guid.CreateVersion7();
        var older = Guid.CreateVersion7();
        var boundaryEvents = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() }.OrderDescending().ToArray();
        foreach (var (eventId, observedAt) in new[]
        {
            (recent, boundary.AddMinutes(1)),
            (boundaryEvents[0], boundary),
            (boundaryEvents[1], boundary),
            (older, boundary.AddMinutes(-1)),
        })
        {
            var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
            await scenario.Complete(operation, CompletedAt, ct);
            await scenario.Record(operation.Scope, eventId, observedAt, ct);
        }

        var otherEvent = Guid.CreateVersion7();
        var other = PostgreSqlInboxDuplicateScenario.Operation("media.integration-event.v1", otherEvent);
        await scenario.Complete(other, CompletedAt, ct);
        await scenario.Record(other.Scope, otherEvent, boundary.AddMinutes(2), ct);

        // Act
        scenario.Commands.Clear();
        var first = await scenario.ReadPage(Consumer, cutoff, eventId: null, limit: 1, cursor: null, ct);
        var pageSql = scenario.Commands.ShouldHaveSingleItem();
        var second = await scenario.ReadPage(Consumer, cutoff, eventId: null, limit: 1, first.Next, ct);
        var last = await scenario.ReadPage(Consumer, cutoff, eventId: null, limit: 1, second.Next, ct);
        var exactOld = await scenario.ReadPage(Consumer, cutoff, older, limit: 20, cursor: null, ct);
        var exactBoundary = await scenario.ReadPage(Consumer, cutoff, boundaryEvents[1], limit: 20, cursor: null, ct);
        var defaultCutoff = await scenario.ReadPage(Consumer, observedSince: null, eventId: null, limit: 20, cursor: null, ct);
        var unfiltered = await scenario.ReadPage(Consumer, ct);

        // Assert
        first.Rows.ShouldHaveSingleItem().EventId.ShouldBe(recent);
        first.Next.ShouldNotBeNull();
        second.Rows.ShouldHaveSingleItem().EventId.ShouldBe(boundaryEvents[0]);
        second.Next.ShouldNotBeNull();
        last.Rows.ShouldHaveSingleItem().EventId.ShouldBe(boundaryEvents[1]);
        last.Next.ShouldBeNull();
        exactOld.Rows.ShouldBeEmpty();
        exactBoundary.Rows.ShouldHaveSingleItem().LastObservedAt.ShouldBe(boundary);
        defaultCutoff.Rows.ShouldBe(unfiltered.Rows);
        defaultCutoff.Next.ShouldBe(unfiltered.Next);
        unfiltered.Rows.Select(row => row.EventId).ShouldContain(older);
        pageSql.ShouldContain("\"LastObservedAt\" >=", StringComparison.Ordinal);
        pageSql.ShouldContain("LIMIT", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retention_preserves_held_owner_and_removes_purged_observations()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var expired = Guid.CreateVersion7();
        var held = Guid.CreateVersion7();
        var purgedOperation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, expired);
        var heldOperation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, held);
        await scenario.Complete(purgedOperation, CompletedAt, ct, owner: "expired-owner");
        await scenario.Complete(heldOperation, CompletedAt, ct, owner: "held-owner");
        await scenario.Record(purgedOperation.Scope, expired, CompletedAt.AddDays(1), ct);
        await scenario.Record(heldOperation.Scope, held, CompletedAt, ct);

        // Act
        await scenario.Purge(CompletedAt.AddSeconds(1), ConsumerScope, ["held-owner"], ct);
        var absent = await scenario.ReadEntry(purgedOperation, ct);
        var survivingEntry = await scenario.ReadEntry(heldOperation, ct);
        var page = await scenario.ReadPage(Consumer, ct);
        var health = await scenario.ReadHealth(Consumer, ct);
        var expiredRecorded = await scenario.Record(purgedOperation.Scope, expired, CompletedAt.AddDays(2), ct);

        // Assert
        absent.ShouldBeNull();
        survivingEntry.ShouldNotBeNull();
        expiredRecorded.ShouldBeFalse();
        page.Rows.ShouldHaveSingleItem().EventId.ShouldBe(held);
        health.ShouldBe(new IntegrationEventInboxDuplicateHealth(1, 1, CompletedAt));
    }

    [Fact]
    public async Task Saturated_counts_keep_tracking_the_latest_observation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        var latest = CompletedAt.AddMinutes(1);
        await scenario.Complete(operation, CompletedAt, ct);
        await scenario.Record(operation.Scope, eventId, CompletedAt, ct);
        await scenario.Saturate(eventId, ct);

        // Act
        var recorded = await scenario.Record(operation.Scope, eventId, latest, ct);
        var health = await scenario.ReadHealth(Consumer, ct);

        // Assert
        recorded.ShouldBeTrue();
        health.ShouldBe(new IntegrationEventInboxDuplicateHealth(1, int.MaxValue, latest));
    }

    [Fact]
    public async Task Canceled_and_rolled_back_observations_leave_no_metadata()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        await scenario.Complete(operation, CompletedAt, ct);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> recordCanceled = () => scenario.Record(operation.Scope, eventId, CompletedAt, cancellation.Token);
        Func<Task> readCanceled = () => scenario.ReadPage(Consumer, cancellation.Token);

        // Act
        var canceled = await recordCanceled.ShouldThrowAssignableTo<OperationCanceledException>();
        var readError = await readCanceled.ShouldThrowAssignableTo<OperationCanceledException>();
        var recordedInTransaction = await scenario.RecordAndRollback(operation.Scope, eventId, CompletedAt, ct);
        var page = await scenario.ReadPage(Consumer, ct);

        // Assert
        canceled.CancellationToken.ShouldBe(cancellation.Token);
        readError.CancellationToken.ShouldBe(cancellation.Token);
        recordedInTransaction.ShouldBeTrue();
        page.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Canceling_a_database_blocked_observation_preserves_the_next_recording()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        await scenario.Complete(operation, CompletedAt, ct);
        var nextTime = CompletedAt.AddMinutes(1);

        // Act
        var blocked = await scenario.CancelBlockedObservation(operation, eventId, CompletedAt, ct);
        var empty = await scenario.ReadHealth(Consumer, ct);
        var next = await scenario.Record(operation.Scope, eventId, nextTime, ct);
        var retained = await scenario.ReadHealth(Consumer, ct);

        // Assert
        blocked.ShouldBeTrue();
        empty.ShouldBe(default);
        next.ShouldBeTrue();
        retained.ShouldBe(new IntegrationEventInboxDuplicateHealth(1, 1, nextTime));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Duplicate_observation_read_rejects_unsupported_page_sizes(int limit)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        scenario.Commands.Clear();
        Func<Task> read = () => scenario.ReadPage(Consumer, ct, limit: limit);

        // Act
        var error = await read.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        error.ParamName.ShouldBe("limit");
        scenario.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Default_inbox_registration_does_not_create_observations()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct, enableObservations: false);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        await scenario.Complete(operation, CompletedAt, ct);
        Func<Task> record = () => scenario.Record(operation.Scope, eventId, CompletedAt, ct);

        // Act
        var error = await record.ShouldThrow<InvalidOperationException>();
        var skipped = await scenario.TryStart(operation, CompletedAt.AddMinutes(1), ct);

        // Assert
        error.Message.ShouldContain("IntegrationEventInboxDuplicateRecorder", StringComparison.Ordinal);
        skipped.ExistingEntry.ShouldNotBeNull().State.ShouldBe(IdempotencyEntryState.Completed);
    }

    [Fact]
    public async Task Opt_in_migration_preserves_legacy_rows_and_adds_only_dependent_storage()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct, initializeLegacy: true);
        var eventId = Guid.CreateVersion7();
        var operation = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        var observationTime = CompletedAt.AddMinutes(1);

        // Act
        var migration = await scenario.CompleteLegacyAndMigrate(operation, CompletedAt, ct);
        var emptyBaseline = await scenario.ReadHealth(Consumer, ct);
        var recorded = await scenario.Record(operation.Scope, eventId, observationTime, ct);
        var entry = await scenario.ReadEntry(operation, ct);
        var health = await scenario.ReadHealth(Consumer, ct);

        // Assert
        var dependent = migration.OfType<CreateTableOperation>().ShouldHaveSingleItem();
        dependent.Name.ShouldBe("observations");
        dependent.Schema.ShouldBe("custom_inbox");
        dependent.ForeignKeys.ShouldHaveSingleItem().PrincipalTable.ShouldBe("operations");
        var additiveChanges = migration.All(change => change is CreateTableOperation or CreateIndexOperation);
        additiveChanges.ShouldBeTrue();
        emptyBaseline.ShouldBe(default);
        recorded.ShouldBeTrue();
        entry.ShouldNotBeNull().ResultFingerprint.ShouldBe("legacy fingerprint");
        health.ShouldBe(new IntegrationEventInboxDuplicateHealth(1, 1, observationTime));
    }

    [Fact]
    public async Task An_observation_requires_an_event_ID()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        scenario.Commands.Clear();
        Func<Task> record = () => scenario.Record(IdempotencyScope.From(ConsumerScope), Guid.Empty, CompletedAt, ct);

        // Act
        var error = await record.ShouldThrowAssignableTo<ArgumentException>();

        // Assert
        error.ParamName.ShouldBe("eventId");
        scenario.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Observation_rejects_a_consumer_name_that_cannot_be_stored_verbatim()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        scenario.Commands.Clear();
        var consumer = new string('a', 201);
        Func<Task> read = () => scenario.ReadHealth(consumer, ct);

        // Act
        var error = await read.ShouldThrow<ArgumentException>();

        // Assert
        error.ParamName.ShouldBe("consumerName");
        scenario.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Observation_cannot_share_the_parent_table()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Func<Task> create = () => PostgreSqlInboxDuplicateScenario.Create(fixture, ct, observationTable: "operations");

        // Act
        var error = await create.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("distinct tables", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("consumer", "inbox.", "custom_inbox.observations", "tableName")]
    [InlineData("", "inbox.", "observations", "consumerName")]
    [InlineData("consumer", "", "observations", "inboxScopePrefix")]
    public void Registration_rejects_invalid_observation_configuration(
        string consumer, string scopePrefix, string tableName, string parameter)
    {
        // Arrange
        Action register = () => PostgreSqlInboxDuplicateScenario.RegisterObservationTable(consumer, scopePrefix, tableName);

        // Act
        var error = register.ShouldThrow<ArgumentException>();

        // Assert
        error.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public async Task Recording_rejects_an_in_memory_provider()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Func<Task> record = () => PostgreSqlInboxDuplicateScenario.RecordWithInMemoryProvider(ct);

        // Act
        var error = await record.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);
    }

    [Fact]
    public async Task First_observation_cannot_assign_an_unbound_scope_to_a_consumer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var unrelated = PostgreSqlInboxDuplicateScenario.Operation("unregistered.integration-event.v1", eventId);
        await scenario.Complete(unrelated, CompletedAt, ct);

        // Act
        scenario.Commands.Clear();
        var recorded = await scenario.Record(unrelated.Scope, eventId, CompletedAt, ct);
        var observedCommands = scenario.Commands.ToArray();
        var health = await scenario.ReadHealth(Consumer, ct);

        // Assert
        recorded.ShouldBeFalse();
        health.ShouldBe(default);
        observedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_second_completed_scope_cannot_rebind_a_consumer_event()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlInboxDuplicateScenario.Create(fixture, ct);
        var eventId = Guid.CreateVersion7();
        var original = PostgreSqlInboxDuplicateScenario.Operation(ConsumerScope, eventId);
        var otherVersion = PostgreSqlInboxDuplicateScenario.Operation("gallery.integration-event.v2", eventId);
        await scenario.Complete(original, CompletedAt, ct);
        await scenario.Complete(otherVersion, CompletedAt, ct);
        await scenario.Record(original.Scope, eventId, CompletedAt, ct);

        // Act
        var recorded = await scenario.Record(otherVersion.Scope, eventId, CompletedAt.AddMinutes(1), ct);
        var page = await scenario.ReadPage(Consumer, ct);

        // Assert
        recorded.ShouldBeFalse();
        page.Rows.ShouldHaveSingleItem().ShouldBe(new IntegrationEventInboxDuplicateRow(eventId, 1, CompletedAt));
    }

    [Theory]
    [InlineData("gallery.")]
    [InlineData("gallery.integration-event.v1")]
    public async Task Registrations_cannot_overlap_the_inbox_scope_of_another_consumer(string overlappingPrefix)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Func<Task> create = () => PostgreSqlInboxDuplicateScenario.Create(fixture, ct, overlappingPrefix: overlappingPrefix);

        // Act
        var error = await create.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("scope prefixes cannot overlap", StringComparison.Ordinal);
    }
}
