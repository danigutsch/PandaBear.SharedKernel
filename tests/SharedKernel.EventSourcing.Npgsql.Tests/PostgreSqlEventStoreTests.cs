using System.Collections.Concurrent;
using System.Diagnostics;
using SharedKernel.Testing;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlEventStoreTests : IAsyncLifetime
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "eventstore";

    private AspireTestApplication? app;
    private string? connectionString;

    public async ValueTask InitializeAsync()
    {
        var appBuilder = DistributedApplication.CreateBuilder([]);
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
        await store.Append(streamId, ExpectedStreamRevision.NoStream, events, TestContext.Current.CancellationToken);
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            envelopes,
            first =>
            {
                Assert.Equal(1, first.Revision.Value);
                Assert.Equal(TestEventSerializer.EventType, first.EventType);
                var eventData = Assert.IsType<TestEvent>(first.Data);
                Assert.Equal("draft-created", eventData.Name);
            },
            second =>
            {
                Assert.Equal(2, second.Revision.Value);
                Assert.Equal(TestEventSerializer.EventType, second.EventType);
                var eventData = Assert.IsType<TestEvent>(second.Data);
                Assert.Equal("published", eventData.Name);
            });
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
        var exception = await Assert.ThrowsAsync<ExpectedStreamRevisionConflictException>(
            () => store.Append(
                streamId,
                ExpectedStreamRevision.NoStream,
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Equal(streamId, exception.StreamId);
        Assert.True(exception.ExpectedRevision.RequiresEmptyStream);
        var actualRevision = Assert.IsType<StreamRevision>(exception.ActualRevision);
        Assert.Equal(1, actualRevision.Value);
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
        Assert.NotNull(savedCheckpoint);
        Assert.Equal("catalog-public-listing", savedCheckpoint.ProjectionName);
        Assert.Equal(27, savedCheckpoint.Position);
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
        Assert.NotNull(savedCheckpoint);
        Assert.Equal(27, savedCheckpoint.Position);
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
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.Save(checkpoint, TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Equal("checkpoint.ProjectionName", exception.ParamName);
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
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.Save(checkpoint, TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Equal("checkpoint.Position", exception.ParamName);
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
        Assert.Equal(1, results.Count(result => result is null));
        Assert.Equal(9, results.OfType<ExpectedStreamRevisionConflictException>().Count());
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        var envelope = Assert.Single(envelopes);
        Assert.Equal(1, envelope.Position);
        Assert.Equal(1, envelope.Revision.Value);
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
        Assert.Equal(10, envelopes.Count);
        Assert.Equal(Enumerable.Range(1, 10), envelopes.Select(envelope => (int)envelope.Revision.Value));
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
        var exception = await Assert.ThrowsAsync<ExpectedStreamRevisionConflictException>(
            () => store.Append(
                streamId,
                ExpectedStreamRevision.From(StreamRevision.From(1)),
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Null(exception.ActualRevision);
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
        var firstEnvelope = Assert.Single(await store.Load(firstStreamId, afterRevision: null, TestContext.Current.CancellationToken));

        // Act
        var envelopes = await store.LoadAfter(firstEnvelope.Position, maxCount: 10, TestContext.Current.CancellationToken);

        // Assert
        var envelope = Assert.Single(envelopes);
        Assert.Equal(secondStreamId, envelope.StreamId);
        Assert.True(envelope.Position > firstEnvelope.Position);
        var eventData = Assert.IsType<TestEvent>(envelope.Data);
        Assert.Equal("second", eventData.Name);
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
        Assert.Equal(5, firstBatch.Count);
        Assert.Equal(5, secondBatch.Count);
        Assert.Equal(10, firstBatch.Concat(secondBatch).Select(envelope => envelope.EventId).Distinct().Count());
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
        Assert.Contains(stoppedActivities, activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityAppend, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeSuccess)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagExpectedRevisionMode, PostgreSqlEventSourcingTelemetry.ExpectedRevisionNoStream));
        Assert.Contains(stoppedActivities, activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityLoad, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagEventCount, 1));
        Assert.Contains(PostgreSqlEventSourcingTelemetry.MetricAppendDuration, measurements, StringComparer.Ordinal);
        Assert.Contains(PostgreSqlEventSourcingTelemetry.MetricLoadDuration, measurements, StringComparer.Ordinal);
        Assert.Contains(PostgreSqlEventSourcingTelemetry.MetricEventsAppended, measurements, StringComparer.Ordinal);
        Assert.Contains(PostgreSqlEventSourcingTelemetry.MetricEventsLoaded, measurements, StringComparer.Ordinal);
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
        _ = await Assert.ThrowsAsync<ExpectedStreamRevisionConflictException>(
            () => store.Append(
                streamId,
                ExpectedStreamRevision.NoStream,
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Contains(stoppedActivities, activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityAppend, StringComparison.Ordinal)
            && activity.Status == ActivityStatusCode.Error
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeConflict)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagActualRevision, 1L));
        Assert.Contains(PostgreSqlEventSourcingTelemetry.MetricAppendConflicts, measurements, StringComparer.Ordinal);
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
        Assert.Contains(stoppedActivities, activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityCheckpoint, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOperation, "save_checkpoint")
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagCheckpointPosition, 27L));
        Assert.Contains(stoppedActivities, activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityCheckpoint, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOperation, "get_checkpoint"));
        Assert.Contains(PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration, measurements, StringComparer.Ordinal);
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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await eventStore.Append(
            StreamId.From("catalog-tour-cancelled-append"),
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await eventStore.Load(
            StreamId.From("catalog-tour-cancelled-load"),
            afterRevision: null,
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await eventStore.LoadAfter(
            position: 0,
            maxCount: 1,
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await checkpointStore.GetCheckpoint(
            "catalog-cancelled-projection",
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await checkpointStore.Save(
            new ProjectionCheckpoint("catalog-cancelled-projection", 1),
            cancellation.Token));

        // Assert
        Assert.DoesNotContain(stoppedActivities, activity =>
            activity.Status == ActivityStatusCode.Error
            || PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeError));
        Assert.DoesNotContain(PostgreSqlEventSourcingTelemetry.MetricAppendDuration, measurements, StringComparer.Ordinal);
        Assert.DoesNotContain(PostgreSqlEventSourcingTelemetry.MetricLoadDuration, measurements, StringComparer.Ordinal);
        Assert.DoesNotContain(PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration, measurements, StringComparer.Ordinal);
    }

    private string ConnectionString => connectionString ?? throw new InvalidOperationException("Fixture is not initialized.");

}
