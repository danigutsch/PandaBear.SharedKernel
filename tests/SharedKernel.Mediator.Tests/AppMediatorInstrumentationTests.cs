using PandaBear.Testing.Metrics;

namespace PandaBear.Mediator.Tests;

/// <summary>
/// Tests that the instruments generated mediator dispatch records into carry the standard names, units, descriptions and, for
/// durations, the shared bucket advice in seconds.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ContractsCapability)]
public sealed class AppMediatorInstrumentationTests
{
    private static readonly double[] SharedDurationBoundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    [Fact]
    public void Duration_histograms_record_seconds_with_the_shared_bucket_boundaries()
    {
        // Arrange
        using var meters = new MeterCollector();

        // Act
        using var instrumentation = new AppMediatorInstrumentation(meters);

        // Assert
        instrumentation.RequestsDuration.Name.ShouldBe("sharedkernel.mediator.request.duration");
        instrumentation.RequestsDuration.Unit.ShouldBe("s");
        instrumentation.RequestsDuration.Description.ShouldBe("Duration of mediator request dispatch.");
        instrumentation.RequestsDuration.Advice.ShouldNotBeNull().HistogramBucketBoundaries.ShouldNotBeNull().ShouldBe(SharedDurationBoundaries);
        instrumentation.NotificationsDuration.Name.ShouldBe("sharedkernel.mediator.notification.duration");
        instrumentation.NotificationsDuration.Unit.ShouldBe("s");
        instrumentation.NotificationsDuration.Description.ShouldBe("Duration of mediator notification fan-out.");
        instrumentation.NotificationsDuration.Advice.ShouldNotBeNull().HistogramBucketBoundaries.ShouldNotBeNull().ShouldBe(SharedDurationBoundaries);
    }

    [Fact]
    public void Counters_use_the_standard_names_and_units()
    {
        // Arrange
        using var meters = new MeterCollector();

        // Act
        using var instrumentation = new AppMediatorInstrumentation(meters);

        // Assert
        instrumentation.RequestsDuration.Meter.Name.ShouldBe("SharedKernel.Mediator");
        instrumentation.RequestsTotal.Name.ShouldBe("sharedkernel.mediator.requests");
        instrumentation.RequestsTotal.Unit.ShouldBe("{request}");
        instrumentation.NotificationsTotal.Name.ShouldBe("sharedkernel.mediator.notifications");
        instrumentation.NotificationsTotal.Unit.ShouldBe("{notification}");
        instrumentation.StreamsTotal.Name.ShouldBe("sharedkernel.mediator.streams");
        instrumentation.StreamsTotal.Unit.ShouldBe("{request}");
    }
}
