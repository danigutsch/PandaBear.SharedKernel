namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Tests that dropped page views are counted under the names of the SharedKernel telemetry standard.</summary>
public sealed class WebAnalyticsTelemetryNameTests
{
    [Fact]
    public void Meter_counter_and_attributes_use_the_standard_names()
    {
        // Act, Assert
        WebAnalyticsTelemetry.MeterName.ShouldBe("SharedKernel.AspNetCore.WebAnalytics");
        WebAnalyticsTelemetry.DroppedPageViewsCounterName.ShouldBe("sharedkernel.web_analytics.page_views.dropped");
        WebAnalyticsTelemetry.TargetTag.ShouldBe("sharedkernel.web_analytics.target");
        WebAnalyticsTelemetry.ReasonTag.ShouldBe("sharedkernel.web_analytics.reason");
    }
}
