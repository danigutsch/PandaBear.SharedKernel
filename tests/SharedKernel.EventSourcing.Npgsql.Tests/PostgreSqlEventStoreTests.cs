using System.Collections.Concurrent;
using System.Diagnostics;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlEventStoreTests : IAsyncLifetime
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "eventstore";

    private AspireTestApplication? app;
    private string? connectionString;

    public async ValueTask InitializeAsync()
    {
        var appBuilder = AspireTestApplication.CreateBuilder();
        var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
        _ = databaseServer.AddDatabase(DatabaseResourceName);

        app = await AspireTestApplication.Start(appBuilder, [PostgreSqlResourceName], null, TestContext.Current.CancellationToken);
        connectionString = await app.GetConnectionString(DatabaseResourceName, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var application = app;
        app = null;
        connectionString = null;

        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }

    [Fact]
    public async Task Append_with_no_stream_loads_persisted_events_in_revision_order()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-test-1");
        var events = new TestEvent[]
        {
            new("draft-created"),
            new("published"),
        };

        // Act
        var appended = await store.Append(streamId, ExpectedStreamRevision.NoStream, events, TestContext.Current.CancellationToken);
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        var persistedEnvelopes = appended.ToArray();
        var loadedEnvelopes = envelopes.ToArray();

        // Assert
        persistedEnvelopes.Length.ShouldBe(2);
        persistedEnvelopes[0].EventId.ShouldBe(loadedEnvelopes[0].EventId);
        persistedEnvelopes[0].Position.ShouldBe(loadedEnvelopes[0].Position);
        persistedEnvelopes[1].EventId.ShouldBe(loadedEnvelopes[1].EventId);
        persistedEnvelopes[1].Position.ShouldBe(loadedEnvelopes[1].Position);
        (envelopes).ShouldMatchCollection(first =>
            {
                (first.Revision.Value).ShouldBe(1);
                (first.EventType).ShouldBe(TestEventSerializer.EventType);
                var eventData = (first.Data).ShouldBeOfType<TestEvent>();
                (eventData.Name).ShouldBe("draft-created");
            }, second =>
            {
                (second.Revision.Value).ShouldBe(2);
                (second.EventType).ShouldBe(TestEventSerializer.EventType);
                var eventData = (second.Data).ShouldBeOfType<TestEvent>();
                (eventData.Name).ShouldBe("published");
            });
    }

    [Fact]
    public async Task Append_returns_the_recorded_at_persisted_for_subsequent_load()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-recorded-at");

        // Act
        var appended = await store.Append(
            streamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            TestContext.Current.CancellationToken);
        var loaded = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);

        // Assert
        appended.ShouldHaveSingleItem().RecordedAt.ShouldBe(loaded.ShouldHaveSingleItem().RecordedAt);
    }

    [Fact]
    public async Task Append_with_stale_expected_revision_reports_conflict()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-test-2");
        await store.Append(
            streamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            TestContext.Current.CancellationToken);

        // Act
        var exception = await ((Func<Task>)(() => store.Append(
                streamId,
                ExpectedStreamRevision.NoStream,
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ExpectedStreamRevisionConflictException>();

        // Assert
        (exception.StreamId).ShouldBe(streamId);
        (exception.ExpectedRevision.RequiresEmptyStream).ShouldBeTrue();
        var actualRevision = (exception.ActualRevision).ShouldBeOfType<StreamRevision>();
        (actualRevision.Value).ShouldBe(1);
    }

    [Fact]
    public async Task Save_upserts_projection_checkpoint()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var firstCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 12);
        var secondCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 27);

        // Act
        await store.Save(firstCheckpoint, TestContext.Current.CancellationToken);
        await store.Save(secondCheckpoint, TestContext.Current.CancellationToken);
        var savedCheckpoint = await store.GetCheckpoint("catalog-public-listing", TestContext.Current.CancellationToken);

        // Assert
        _ = (savedCheckpoint).ShouldNotBeNull();
        (savedCheckpoint.ProjectionName).ShouldBe("catalog-public-listing");
        (savedCheckpoint.Position).ShouldBe(27);
    }

    [Fact]
    public async Task Save_does_not_move_projection_checkpoint_backward()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var currentCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 27);
        var staleCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 12);

        // Act
        await store.Save(currentCheckpoint, TestContext.Current.CancellationToken);
        await store.Save(staleCheckpoint, TestContext.Current.CancellationToken);
        var savedCheckpoint = await store.GetCheckpoint("catalog-public-listing", TestContext.Current.CancellationToken);

        // Assert
        _ = (savedCheckpoint).ShouldNotBeNull();
        (savedCheckpoint.Position).ShouldBe(27);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_rejects_missing_projection_name(string projectionName)
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var checkpoint = new ProjectionCheckpoint(projectionName, 12);

        // Act
        var exception = await ((Func<Task>)(() => store.Save(checkpoint, TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ArgumentException>();

        // Assert
        (exception.ParamName).ShouldBe("checkpoint.ProjectionName");
    }

    [Fact]
    public async Task Save_rejects_negative_position()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var checkpoint = new ProjectionCheckpoint("catalog-public-listing", -1);

        // Act
        var exception = await ((Func<Task>)(() => store.Save(checkpoint, TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        (exception.ParamName).ShouldBe("checkpoint.Position");
    }

    [Fact]
    public async Task Append_with_concurrent_no_stream_writers_reports_conflicts()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-concurrent");

        // Act
        var appendTasks = Enumerable.Range(1, 10)
            .Select(index => PostgreSqlEventStoreTestsHelpers.CaptureAppendResult(store, streamId, new TestEvent($"event-{index}")))
            .ToArray();
        var results = await Task.WhenAll(appendTasks);

        // Assert
        (results.Count(result => result is null)).ShouldBe(1);
        (results.OfType<ExpectedStreamRevisionConflictException>().Count()).ShouldBe(9);
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        var envelope = (envelopes).ShouldHaveSingleItem();
        (envelope.Position).ShouldBe(1);
        (envelope.Revision.Value).ShouldBe(1);
    }

    [Fact]
    public async Task Append_with_concurrent_any_writers_appends_all_events()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-any-concurrent");

        // Act
        var appendTasks = Enumerable.Range(1, 10)
            .Select(index => store.Append(
                streamId,
                ExpectedStreamRevision.Any,
                [new TestEvent($"event-{index}")],
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();
        await Task.WhenAll(appendTasks);

        // Assert
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        (envelopes.Count).ShouldBe(10);
        (envelopes.Select(envelope => (int)envelope.Revision.Value)).ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task Append_with_specific_revision_after_empty_stream_reports_conflict()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-empty-conflict");

        // Act
        var exception = await ((Func<Task>)(() => store.Append(
                streamId,
                ExpectedStreamRevision.From(StreamRevision.From(1)),
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ExpectedStreamRevisionConflictException>();

        // Assert
        (exception.ActualRevision).ShouldBeNull();
    }

    [Fact]
    public async Task Load_after_loads_events_in_global_position_order()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var firstStreamId = StreamId.From("catalog-tour-global-1");
        var secondStreamId = StreamId.From("catalog-tour-global-2");
        await store.Append(
            firstStreamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("first")],
            TestContext.Current.CancellationToken);
        await store.Append(
            secondStreamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("second")],
            TestContext.Current.CancellationToken);
        var firstEnvelope = (await store.Load(firstStreamId, afterRevision: null, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        // Act
        var envelopes = await store.LoadAfter(firstEnvelope.Position, maxCount: 10, TestContext.Current.CancellationToken);

        // Assert
        var envelope = (envelopes).ShouldHaveSingleItem();
        (envelope.StreamId).ShouldBe(secondStreamId);
        (envelope.Position > firstEnvelope.Position).ShouldBeTrue();
        var eventData = (envelope.Data).ShouldBeOfType<TestEvent>();
        (eventData.Name).ShouldBe("second");
    }

    [Fact]
    public async Task Load_after_can_checkpoint_concurrent_cross_stream_appends()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);

        // Act
        var appendTasks = Enumerable.Range(1, 10)
            .Select(index => store.Append(
                StreamId.From($"catalog-tour-global-concurrent-{index}"),
                ExpectedStreamRevision.NoStream,
                [new TestEvent($"event-{index}")],
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();
        await Task.WhenAll(appendTasks);
        var firstBatch = await store.LoadAfter(position: 0, maxCount: 5, TestContext.Current.CancellationToken);
        var checkpoint = firstBatch.Max(envelope => envelope.Position);
        var secondBatch = await store.LoadAfter(checkpoint, maxCount: 10, TestContext.Current.CancellationToken);

        // Assert
        (firstBatch.Count).ShouldBe(5);
        (secondBatch.Count).ShouldBe(5);
        (firstBatch.Concat(secondBatch).Select(envelope => envelope.EventId).Distinct().Count()).ShouldBe(10);
    }

    [Fact]
    public async Task Append_and_load_emit_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(stoppedActivities);
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-telemetry");

        // Act
        await store.Append(
            streamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            TestContext.Current.CancellationToken);
        _ = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);

        // Assert
        (stoppedActivities).ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityAppend, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeSuccess)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagExpectedRevisionMode, PostgreSqlEventSourcingTelemetry.ExpectedRevisionNoStream));
        (stoppedActivities).ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityLoad, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagEventCount, 1));
        (measurements).ShouldContain(PostgreSqlEventSourcingTelemetry.MetricAppendDuration, StringComparer.Ordinal);
        (measurements).ShouldContain(PostgreSqlEventSourcingTelemetry.MetricLoadDuration, StringComparer.Ordinal);
        (measurements).ShouldContain(PostgreSqlEventSourcingTelemetry.MetricEventsAppended, StringComparer.Ordinal);
        (measurements).ShouldContain(PostgreSqlEventSourcingTelemetry.MetricEventsLoaded, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Append_conflict_emits_error_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(stoppedActivities);
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-telemetry-conflict");
        await store.Append(
            streamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            TestContext.Current.CancellationToken);

        // Act
        _ = await ((Func<Task>)(() => store.Append(
                streamId,
                ExpectedStreamRevision.NoStream,
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ExpectedStreamRevisionConflictException>();

        // Assert
        (stoppedActivities).ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityAppend, StringComparison.Ordinal)
            && activity.Status == ActivityStatusCode.Error
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeConflict)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagActualRevision, 1L));
        (measurements).ShouldContain(PostgreSqlEventSourcingTelemetry.MetricAppendConflicts, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Checkpoint_store_emits_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(stoppedActivities);
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var checkpoint = new ProjectionCheckpoint("catalog-public-listing", 27);

        // Act
        await store.Save(checkpoint, TestContext.Current.CancellationToken);
        _ = await store.GetCheckpoint(checkpoint.ProjectionName, TestContext.Current.CancellationToken);

        // Assert
        (stoppedActivities).ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityCheckpoint, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOperation, "save_checkpoint")
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagCheckpointPosition, 27L));
        (stoppedActivities).ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityCheckpoint, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOperation, "get_checkpoint"));
        (measurements).ShouldContain(PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Cancelled_operations_do_not_emit_error_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(stoppedActivities);
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var eventStore = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await using var checkpointStore = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await eventStore.Initialize(TestContext.Current.CancellationToken);
        await checkpointStore.Initialize(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        await ((Func<Task>)(async () => await eventStore.Append(
            StreamId.From("catalog-tour-cancelled-append"),
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            cancellation.Token))).ShouldThrowAssignableTo<OperationCanceledException>();
        await ((Func<Task>)(async () => await eventStore.Load(
            StreamId.From("catalog-tour-cancelled-load"),
            afterRevision: null,
            cancellation.Token))).ShouldThrowAssignableTo<OperationCanceledException>();
        await ((Func<Task>)(async () => await eventStore.LoadAfter(
            position: 0,
            maxCount: 1,
            cancellation.Token))).ShouldThrowAssignableTo<OperationCanceledException>();
        await ((Func<Task>)(async () => await checkpointStore.GetCheckpoint(
            "catalog-cancelled-projection",
            cancellation.Token))).ShouldThrowAssignableTo<OperationCanceledException>();
        await ((Func<Task>)(async () => await checkpointStore.Save(
            new ProjectionCheckpoint("catalog-cancelled-projection", 1),
            cancellation.Token))).ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        (stoppedActivities).ShouldNotContain(activity =>
            activity.Status == ActivityStatusCode.Error
            || PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeError));
        (measurements).ShouldNotContain(PostgreSqlEventSourcingTelemetry.MetricAppendDuration, StringComparer.Ordinal);
        (measurements).ShouldNotContain(PostgreSqlEventSourcingTelemetry.MetricLoadDuration, StringComparer.Ordinal);
        (measurements).ShouldNotContain(PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration, StringComparer.Ordinal);
    }

    private string ConnectionString => connectionString ?? throw new InvalidOperationException("Fixture is not initialized.");

}
