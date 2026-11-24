namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>Tests that the event-sourcing telemetry uses the names, units and duration advice of the SharedKernel telemetry standard.</summary>
public sealed class PostgreSqlEventSourcingTelemetryNameTests
{
    private static readonly double[] SharedDurationBoundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    [Fact]
    public void Spans_instruments_and_attributes_use_the_standard_names()
    {
        // Act, Assert
        PostgreSqlEventSourcingTelemetry.Name.ShouldBe("SharedKernel.EventSourcing.Npgsql");
        PostgreSqlEventSourcingTelemetry.ActivityAppend.ShouldBe("event_sourcing.append");
        PostgreSqlEventSourcingTelemetry.ActivityLoad.ShouldBe("event_sourcing.load");
        PostgreSqlEventSourcingTelemetry.ActivityCheckpoint.ShouldBe("event_sourcing.checkpoint");
        PostgreSqlEventSourcingTelemetry.MetricAppendDuration.ShouldBe("sharedkernel.event_sourcing.append.duration");
        PostgreSqlEventSourcingTelemetry.MetricLoadDuration.ShouldBe("sharedkernel.event_sourcing.load.duration");
        PostgreSqlEventSourcingTelemetry.MetricCheckpointDuration.ShouldBe("sharedkernel.event_sourcing.checkpoint.duration");
        PostgreSqlEventSourcingTelemetry.MetricEventsAppended.ShouldBe("sharedkernel.event_sourcing.events.appended");
        PostgreSqlEventSourcingTelemetry.MetricEventsLoaded.ShouldBe("sharedkernel.event_sourcing.events.loaded");
        PostgreSqlEventSourcingTelemetry.MetricAppendConflicts.ShouldBe("sharedkernel.event_sourcing.append.conflicts");
        PostgreSqlEventSourcingTelemetry.TagSchema.ShouldBe("sharedkernel.event_sourcing.schema");
        PostgreSqlEventSourcingTelemetry.TagOperation.ShouldBe("sharedkernel.event_sourcing.operation");
        PostgreSqlEventSourcingTelemetry.TagOutcome.ShouldBe("sharedkernel.event_sourcing.outcome");
        PostgreSqlEventSourcingTelemetry.TagEventCount.ShouldBe("sharedkernel.event_sourcing.event_count");
        PostgreSqlEventSourcingTelemetry.TagExpectedRevisionMode.ShouldBe("sharedkernel.event_sourcing.expected_revision.mode");
        PostgreSqlEventSourcingTelemetry.TagExpectedRevisionValue.ShouldBe("sharedkernel.event_sourcing.expected_revision.value");
        PostgreSqlEventSourcingTelemetry.TagActualRevision.ShouldBe("sharedkernel.event_sourcing.actual_revision");
        PostgreSqlEventSourcingTelemetry.TagProjectionName.ShouldBe("sharedkernel.event_sourcing.projection.name");
        PostgreSqlEventSourcingTelemetry.TagCheckpointPosition.ShouldBe("sharedkernel.event_sourcing.checkpoint.position");
        PostgreSqlEventSourcingTelemetry.TagDbSystemName.ShouldBe("db.system.name");
        PostgreSqlEventSourcingTelemetry.DbSystemNamePostgreSql.ShouldBe("postgresql");
    }

    [Fact]
    public void Duration_histograms_record_seconds_with_the_shared_bucket_boundaries()
    {
        // Act
        var durations = new[]
        {
            PostgreSqlEventSourcingTelemetry.AppendDuration,
            PostgreSqlEventSourcingTelemetry.LoadDuration,
            PostgreSqlEventSourcingTelemetry.CheckpointDuration,
        };

        // Assert
        foreach (var histogram in durations)
        {
            histogram.Unit.ShouldBe("s");
            histogram.Advice.ShouldNotBeNull().HistogramBucketBoundaries.ShouldNotBeNull().ShouldBe(SharedDurationBoundaries);
        }
    }
}
