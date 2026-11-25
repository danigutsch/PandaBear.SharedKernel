namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>Verifies durable causation metadata on PostgreSQL event envelopes.</summary>
/// <param name="fixture">The shared PostgreSQL server fixture.</param>
public sealed class PostgreSqlEventCausationTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Initialize_upgrades_existing_events_without_inventing_causation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await using var store = new PostgreSqlEventStore(dataSource, new TestEventSerializer(), options);
        await store.Initialize(ct);
        var stream = StreamId.From("legacy-event-stream");
        var persisted = await store.Append(stream, ExpectedStreamRevision.NoStream, [new TestEvent("legacy")], ct);
        var legacy = persisted.ShouldHaveSingleItem();
        await using var restoreLegacyShape = dataSource.CreateCommand($"ALTER TABLE {options.Schema}.events DROP COLUMN IF EXISTS causation_event_id");
        _ = await restoreLegacyShape.ExecuteNonQueryAsync(ct);

        // Act
        await store.Initialize(ct);
        await store.Initialize(ct);
        await using var readExisting = dataSource.CreateCommand($"SELECT causation_event_id FROM {options.Schema}.events WHERE event_id = @eventId");
        readExisting.Parameters.AddWithValue("eventId", legacy.EventId);
        var recordedCausation = await readExisting.ExecuteScalarAsync(ct);

        // Assert
        recordedCausation.ShouldBe(DBNull.Value);
        var loaded = await store.Load(stream, afterRevision: null, ct);
        loaded.ShouldHaveSingleItem().ShouldBe(legacy);
        legacy.CausationEventId.ShouldBeNull();
    }

    [Fact]
    public async Task Append_persists_the_exact_cause_for_each_event_and_both_read_paths()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(ct);
        IEventStore eventStore = store;
        var stream = StreamId.From("affected-stream");
        var prior = await eventStore.Append(stream, ExpectedStreamRevision.NoStream, [new TestEvent("accepted")], ct);
        var previousEvent = prior.ShouldHaveSingleItem();
        var triggeringEventId = Guid.NewGuid();
        var expectedRevision = ExpectedStreamRevision.From(previousEvent.Revision);

        // Act
        var appended = await eventStore.Append(stream, expectedRevision,
            [new TestEvent("applied"), new TestEvent("recorded")], triggeringEventId, ct);
        var loaded = await eventStore.Load(stream, previousEvent.Revision, ct);
        var projection = await eventStore.LoadAfter(previousEvent.Position, maxCount: 10, ct);

        // Assert
        appended.Select(item => item.CausationEventId).ShouldBe([triggeringEventId, triggeringEventId]);
        loaded.ShouldBe(appended);
        projection.ShouldBe(appended);
        previousEvent.CausationEventId.ShouldBeNull();
    }

    [Fact]
    public async Task Unsuccessful_causal_appends_leave_the_stream_and_global_positions_unchanged()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(ct);
        var stream = StreamId.From("unchanged-stream");
        var original = await store.Append(stream, ExpectedStreamRevision.NoStream, [new TestEvent("accepted")], ct);
        var committed = original.ShouldHaveSingleItem();
        var triggeringEventId = Guid.NewGuid();
        var events = new TestEvent[] { new("applied") };

        // Act
        var conflict = await ShouldAssertionExtensions.ShouldThrow<ExpectedStreamRevisionConflictException>((Func<Task>)(() =>
            store.Append(stream, ExpectedStreamRevision.NoStream, events, triggeringEventId, ct).AsTask()));
        var revision = ExpectedStreamRevision.From(committed.Revision);
        await ShouldAssertionExtensions.ShouldThrow<OperationCanceledException>((Func<Task>)(() =>
            store.Append(stream, revision, events, triggeringEventId, new CancellationToken(canceled: true)).AsTask()));
        var invalidCause = await ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<Task>)(() =>
            store.Append(stream, revision, events, Guid.Empty, ct).AsTask()));
        var loaded = await store.Load(stream, afterRevision: null, ct);
        var following = await store.LoadAfter(committed.Position, maxCount: 10, ct);

        // Assert
        conflict.ActualRevision.ShouldBe(committed.Revision);
        invalidCause.ParamName.ShouldBe("causationEventId");
        loaded.ShouldHaveSingleItem().ShouldBe(committed);
        following.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancellation_during_second_event_rolls_back_the_causal_batch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var appendCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var serializer = new CancelOnSecondEventSerializer(appendCancellation);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, serializer, options);
        await store.Initialize(ct);
        var stream = StreamId.From("cancelled-batch");
        var cause = Guid.NewGuid();

        // Act
        await ShouldAssertionExtensions.ShouldThrow<OperationCanceledException>((Func<Task>)(() =>
            store.Append(stream, ExpectedStreamRevision.NoStream,
                [new TestEvent("first"), new TestEvent("second")], cause, appendCancellation.Token).AsTask()));
        var visibleAfterCancellation = await store.Load(stream, afterRevision: null, ct);
        var globallyVisible = await store.LoadAfter(position: 0, maxCount: 10, ct);
        var retried = await store.Append(stream, ExpectedStreamRevision.NoStream, [new TestEvent("retry")], cause, ct);

        // Assert
        appendCancellation.IsCancellationRequested.ShouldBeTrue();
        visibleAfterCancellation.ShouldBeEmpty();
        globallyVisible.ShouldBeEmpty();
        var committed = retried.ShouldHaveSingleItem();
        committed.CausationEventId.ShouldBe(cause);
        committed.Revision.Value.ShouldBe(1);
    }

    [Fact]
    public async Task Legacy_provider_fails_explicitly_instead_of_dropping_causation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var legacy = new LegacyEventStore();
        IEventStore store = legacy;
        var stream = StreamId.From("legacy-projection");
        var cause = Guid.NewGuid();
        var events = new TestEvent[] { new("accepted") };

        // Act
        _ = await store.Append(stream, ExpectedStreamRevision.NoStream, events, ct);
        await ShouldAssertionExtensions.ShouldThrow<NotSupportedException>((Func<Task>)(() =>
            store.Append(stream, ExpectedStreamRevision.NoStream, events, cause, ct).AsTask()));

        // Assert
        legacy.Appends.ShouldBe(1);
    }
}
