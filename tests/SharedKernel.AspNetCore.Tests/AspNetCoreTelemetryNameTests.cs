namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests that the ASP.NET Core error and security telemetry uses the names of the SharedKernel telemetry standard.</summary>
public sealed class AspNetCoreTelemetryNameTests
{
    [Fact]
    public void Meter_instruments_attributes_and_event_IDs_use_the_standard_names()
    {
        // Act, Assert
        AspNetCoreTelemetry.MeterName.ShouldBe("SharedKernel.AspNetCore");
        AspNetCoreTelemetry.UnhandledExceptionsCounterName.ShouldBe("sharedkernel.aspnetcore.unhandled_exceptions");
        AspNetCoreTelemetry.AuthenticationEventsCounterName.ShouldBe("sharedkernel.aspnetcore.authentication.events");
        AspNetCoreTelemetry.RateLimitRejectionsCounterName.ShouldBe("sharedkernel.aspnetcore.rate_limit.rejections");
        AspNetCoreTelemetry.ErrorTypeTag.ShouldBe("error.type");
        AspNetCoreTelemetry.RouteTemplateTag.ShouldBe("http.route");
        AspNetCoreTelemetry.HandledTag.ShouldBe("sharedkernel.aspnetcore.handled");
        AspNetCoreTelemetry.EventTag.ShouldBe("sharedkernel.aspnetcore.authentication.event");
        AspNetCoreTelemetry.ReasonTag.ShouldBe("sharedkernel.aspnetcore.authentication.reason");
        AspNetCoreTelemetry.SchemeTag.ShouldBe("sharedkernel.aspnetcore.authentication.scheme");
        AspNetCoreTelemetry.PolicyTag.ShouldBe("sharedkernel.aspnetcore.rate_limit.policy");
        AspNetCoreTelemetry.UnhandledExceptionEventId.ShouldBe(4001);
        AspNetCoreTelemetry.AuthenticationEventId.ShouldBe(4002);
        AspNetCoreTelemetry.RateLimitRejectionEventId.ShouldBe(4003);
    }
}
