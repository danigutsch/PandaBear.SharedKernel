using Microsoft.Extensions.Logging.Abstractions;

namespace PandaBear.Scheduling.Tests;

/// <summary>Tests the names, units, duration advice and attributes of the polling telemetry.</summary>
public sealed class SchedulingTelemetryTests
{
    private static readonly double[] SharedDurationBoundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    [Fact]
    public void Source_meter_instruments_and_attributes_use_the_standard_names()
    {
        // Act, Assert
        SchedulingTelemetry.Name.ShouldBe("SharedKernel.Scheduling");
        SchedulingTelemetry.ActivitySource.Name.ShouldBe("SharedKernel.Scheduling");
        SchedulingTelemetry.Meter.Name.ShouldBe("SharedKernel.Scheduling");
        SchedulingTelemetry.ActivityPollingCycle.ShouldBe("scheduling.poll");
        SchedulingTelemetry.MetricPollingCycleDuration.ShouldBe("sharedkernel.scheduling.polling.cycle.duration");
        SchedulingTelemetry.MetricPollingBatches.ShouldBe("sharedkernel.scheduling.polling.batches");
        SchedulingTelemetry.MetricPollingItems.ShouldBe("sharedkernel.scheduling.polling.items");
        SchedulingTelemetry.MetricPollingFailures.ShouldBe("sharedkernel.scheduling.polling.failures");
        SchedulingTelemetry.TagServiceName.ShouldBe("sharedkernel.scheduling.service.name");
        SchedulingTelemetry.TagOutcome.ShouldBe("sharedkernel.scheduling.outcome");
        SchedulingTelemetry.TagItemCount.ShouldBe("sharedkernel.scheduling.item_count");
        SchedulingTelemetry.TagErrorType.ShouldBe("error.type");
    }

    [Fact]
    public void Instruments_carry_units_descriptions_and_the_shared_duration_advice()
    {
        // Act
        var duration = SchedulingTelemetry.PollingCycleDuration;

        // Assert
        duration.Unit.ShouldBe("s");
        duration.Description.ShouldBe("Duration of one polling drain cycle.");
        duration.Advice.ShouldNotBeNull().HistogramBucketBoundaries.ShouldNotBeNull().ShouldBe(SharedDurationBoundaries);
        SchedulingTelemetry.PollingBatches.Unit.ShouldBe("{batch}");
        SchedulingTelemetry.PollingBatches.Description.ShouldBe("Completed polling batches.");
        SchedulingTelemetry.PollingItems.Unit.ShouldBe("{item}");
        SchedulingTelemetry.PollingItems.Description.ShouldBe("Processed polling work items.");
        SchedulingTelemetry.PollingFailures.Unit.ShouldBe("{failure}");
        SchedulingTelemetry.PollingFailures.Description.ShouldBe("Non-fatal polling failures.");
    }

    [Fact]
    public async Task A_drained_cycle_emits_a_span_and_measurements_with_the_standard_attributes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string serviceName = "telemetry-drain";
        using var capture = new SchedulingTelemetryCapture(serviceName);
        using var service = new ScriptedPollingService(NullLogger.Instance, serviceName, TimeSpan.FromHours(1), 2, 0);

        // Act
        await service.StartAsync(ct);
        await service.ScriptConsumed.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await service.StopAsync(ct);

        // Assert
        var span = capture.Activities.ShouldHaveSingleItem();
        span.OperationName.ShouldBe("scheduling.poll");
        span.GetTagItem("sharedkernel.scheduling.outcome").ShouldBe("success");
        span.GetTagItem("sharedkernel.scheduling.item_count").ShouldBe(2);
        var duration = capture.Measurements.ShouldHaveSingleItem(static measurement => measurement.Instrument == "sharedkernel.scheduling.polling.cycle.duration");
        duration.Tags.GetValueOrDefault("sharedkernel.scheduling.outcome").ShouldBe("success");
        duration.Value.ShouldBeLessThan(30, "the duration is recorded in seconds");
        capture.Measurements.Count(static measurement => measurement.Instrument == "sharedkernel.scheduling.polling.batches").ShouldBe(2);
        capture.Measurements.ShouldHaveSingleItem(static measurement => measurement.Instrument == "sharedkernel.scheduling.polling.items").Value.ShouldBe(2);
    }

    [Fact]
    public async Task A_failing_cycle_reports_the_full_exception_type()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string serviceName = "telemetry-failure";
        using var capture = new SchedulingTelemetryCapture(serviceName);
        using var service = new ScriptedPollingService(NullLogger.Instance, serviceName, TimeSpan.FromHours(1), new InvalidOperationException("secret detail"));

        // Act
        await service.StartAsync(ct);
        await service.ScriptConsumed.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await service.StopAsync(ct);

        // Assert
        var span = capture.Activities.ShouldHaveSingleItem();
        span.GetTagItem("sharedkernel.scheduling.outcome").ShouldBe("error");
        span.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        var failure = capture.Measurements.ShouldHaveSingleItem(static measurement => measurement.Instrument == "sharedkernel.scheduling.polling.failures");
        failure.Tags.GetValueOrDefault("error.type").ShouldBe("System.InvalidOperationException");
        capture.Measurements.ShouldHaveSingleItem(static measurement => measurement.Instrument == "sharedkernel.scheduling.polling.cycle.duration")
            .Tags.GetValueOrDefault("sharedkernel.scheduling.outcome").ShouldBe("error");
    }
}
