using System.Collections.Concurrent;
using System.Diagnostics;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlEventSourcingTelemetryTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Concurrent_telemetry_listeners_capture_only_their_trace_and_schema()
    {
        // Arrange
        var firstOptions = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        var secondOptions = PostgreSqlEventStoreTestsHelpers.CreateOptions();

        // Act
        var captures = await Task.WhenAll(
            PostgreSqlEventStoreTestsHelpers.CaptureTelemetry(
                ConnectionString,
                firstOptions,
                "first-telemetry-stream"),
            PostgreSqlEventStoreTestsHelpers.CaptureTelemetry(
                ConnectionString,
                secondOptions,
                "second-telemetry-stream"));

        // Assert
        captures[0].Activities.Length.ShouldBe(2);
        captures[1].Activities.Length.ShouldBe(2);
        captures[0].Measurements.Length.ShouldBe(4);
        captures[1].Measurements.Length.ShouldBe(4);
    }

    [Fact]
    public async Task Append_and_load_emit_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var rootActivity = new Activity("append-and-load-test");
        _ = rootActivity.Start();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(
            stoppedActivities,
            rootActivity);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements, options.Schema);
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
        stoppedActivities.ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityAppend, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeSuccess)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagExpectedRevisionMode, PostgreSqlEventSourcingTelemetry.ExpectedRevisionNoStream));
        stoppedActivities.ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityLoad, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagEventCount, 1));
        measurements.ShouldContain(PostgreSqlEventSourcingTelemetry.MetricAppendDuration, StringComparer.Ordinal);
        measurements.ShouldContain(PostgreSqlEventSourcingTelemetry.MetricLoadDuration, StringComparer.Ordinal);
        measurements.ShouldContain(PostgreSqlEventSourcingTelemetry.MetricEventsAppended, StringComparer.Ordinal);
        measurements.ShouldContain(PostgreSqlEventSourcingTelemetry.MetricEventsLoaded, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Append_conflict_emits_error_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var rootActivity = new Activity("append-conflict-test");
        _ = rootActivity.Start();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(
            stoppedActivities,
            rootActivity);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements, options.Schema);
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
                TestContext.Current.CancellationToken).AsTask()))
            .ShouldThrow<ExpectedStreamRevisionConflictException>();

        // Assert
        stoppedActivities.ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityAppend, StringComparison.Ordinal)
            && activity.Status == ActivityStatusCode.Error
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeConflict)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagActualRevision, 1L));
        measurements.ShouldContain(PostgreSqlEventSourcingTelemetry.MetricAppendConflicts, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Checkpoint_store_emits_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var rootActivity = new Activity("checkpoint-test");
        _ = rootActivity.Start();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(
            stoppedActivities,
            rootActivity);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements, options.Schema);
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var checkpoint = new ProjectionCheckpoint("catalog-public-listing", 27);

        // Act
        await store.Save(checkpoint, TestContext.Current.CancellationToken);
        _ = await store.GetCheckpoint(checkpoint.ProjectionName, TestContext.Current.CancellationToken);

        // Assert
        stoppedActivities.ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityCheckpoint, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOperation, "save_checkpoint")
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagCheckpointPosition, 27L));
        stoppedActivities.ShouldContain(activity =>
            string.Equals(activity.OperationName, PostgreSqlEventSourcingTelemetry.ActivityCheckpoint, StringComparison.Ordinal)
            && PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOperation, "get_checkpoint"));
        measurements.ShouldContain(PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Cancelled_operations_do_not_emit_error_telemetry()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var rootActivity = new Activity("cancelled-operations-test");
        _ = rootActivity.Start();
        using var activityListener = PostgreSqlEventStoreTestsHelpers.CreateActivityListener(
            stoppedActivities,
            rootActivity);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        using var meterListener = PostgreSqlEventStoreTestsHelpers.CreateMeterListener(measurements, options.Schema);
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
        stoppedActivities.ShouldNotContain(activity =>
            activity.Status == ActivityStatusCode.Error
            || PostgreSqlEventStoreTestsHelpers.HasTag(activity, PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeError));
        measurements.ShouldNotContain(PostgreSqlEventSourcingTelemetry.MetricAppendDuration, StringComparer.Ordinal);
        measurements.ShouldNotContain(PostgreSqlEventSourcingTelemetry.MetricLoadDuration, StringComparer.Ordinal);
        measurements.ShouldNotContain(PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration, StringComparer.Ordinal);
    }
}
