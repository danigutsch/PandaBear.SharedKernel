namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests that dropped page views are counted under the names of the SharedKernel telemetry standard.</summary>
public sealed class WebAnalyticsTelemetryNameTests
{
    [Fact]
    public void Meter_counter_attributes_and_event_IDs_use_the_standard_names()
    {
        // Act, Assert
        WebAnalyticsTelemetry.MeterName.ShouldBe("SharedKernel.AspNetCore.WebAnalytics");
        WebAnalyticsTelemetry.DroppedPageViewsCounterName.ShouldBe("sharedkernel.web_analytics.page_views.dropped");
        WebAnalyticsTelemetry.TargetTag.ShouldBe("sharedkernel.web_analytics.target");
        WebAnalyticsTelemetry.ReasonTag.ShouldBe("sharedkernel.web_analytics.reason");
        WebAnalyticsTelemetry.SendRejectedEventId.ShouldBe(4201);
        WebAnalyticsTelemetry.SendFailedEventId.ShouldBe(4202);
        WebAnalyticsTelemetry.UnsentAtShutdownEventId.ShouldBe(4204);
        WebAnalyticsTelemetry.PageViewsOffEventId.ShouldBe(4205);
    }
}
