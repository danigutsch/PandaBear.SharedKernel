using System.Net;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.RateLimiting.Tests;

/// <summary>Tests the rate-limit rejection event through the real rate-limiter middleware.</summary>
public sealed class RateLimitEventPipelineTests
{
    [Fact]
    public async Task Rate_limited_request_is_reported_with_the_policy_name_through_the_real_middleware()
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var logger = new RecordingLogger<SecurityEventReporter>();
        await using var app = await SecurityEventTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };
        using var first = await client.GetAsync(new Uri("/login", UriKind.Relative), TestContext.Current.CancellationToken);

        // Act
        using var second = await client.GetAsync(new Uri("/login", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe((HttpStatusCode)StatusCodes.Status429TooManyRequests);
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(AspNetCoreTelemetry.RateLimitRejectionsCounterName);
        measurement.Tags[AspNetCoreTelemetry.PolicyTag].ShouldBe("login");
    }
}
