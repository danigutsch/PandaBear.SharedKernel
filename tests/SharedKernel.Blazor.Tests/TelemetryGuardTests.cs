namespace SharedKernel.Blazor.Tests;

/// <summary>Tests the telemetry guard that the Blazor package links from SharedKernel.AspNetCore.</summary>
public sealed class TelemetryGuardTests
{
    [Fact]
    public void A_failing_telemetry_action_is_contained()
    {
        // Act
        var failure = Record.Exception(() => TelemetryGuard.Run(() => throw new InvalidOperationException("meter factory failed")));

        // Assert
        failure.ShouldBeNull();
    }
}
