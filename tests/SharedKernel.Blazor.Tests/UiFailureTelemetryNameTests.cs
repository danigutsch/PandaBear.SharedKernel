namespace PandaBear.Blazor.Tests;

/// <summary>Tests that handled UI failures are counted under the names of the SharedKernel telemetry standard.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class UiFailureTelemetryNameTests
{
    [Fact]
    public void Meter_counter_and_attributes_use_the_standard_names()
    {
        // Act, Assert
        UiFailureReporter.MeterName.ShouldBe("SharedKernel.Blazor");
        UiFailureReporter.HandledFailuresCounterName.ShouldBe("sharedkernel.blazor.handled_failures");
        UiFailureReporter.ErrorTypeTag.ShouldBe("error.type");
        UiFailureReporter.OperationTag.ShouldBe("sharedkernel.blazor.operation");
    }
}
