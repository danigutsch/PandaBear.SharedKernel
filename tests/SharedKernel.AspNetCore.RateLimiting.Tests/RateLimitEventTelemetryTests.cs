using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.RateLimiting.Tests;

/// <summary>Tests the opt-in rate-limit rejection events.</summary>
public sealed class RateLimitEventTelemetryTests
{
    [Fact]
    public async Task Rate_limit_rejection_is_logged_and_counted_with_the_policy_name_and_keeps_the_existing_callback()
    {
        // Arrange
        var callbackRan = false;
        using var fixture = SecurityEventTestFixture.Create(rateLimiter: options =>
        {
            options.AddRemoteIpFixedWindowPolicy("login", permitLimit: 1, window: TimeSpan.FromMinutes(1));
            options.OnRejected = (_, _) =>
            {
                callbackRan = true;
                return ValueTask.CompletedTask;
            };
        });
        var httpContext = fixture.HttpContext();
        httpContext.SetEndpoint(new Endpoint(
            requestDelegate: null,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute("login")),
            displayName: "test"));
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var rejected = fixture.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.OnRejected.ShouldNotBeNull();

        // Act
        await rejected(new OnRejectedContext { HttpContext = httpContext, Lease = new RejectedRateLimitLease() }, TestContext.Current.CancellationToken);

        // Assert
        callbackRan.ShouldBeTrue("the host's own callback must keep running");
        var measurement = fixture.Meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(AspNetCoreTelemetry.RateLimitRejectionsCounterName);
        measurement.Tags.Keys.ShouldBeEquivalentTo(AspNetCoreTelemetry.PolicyTag);
        measurement.Tags[AspNetCoreTelemetry.PolicyTag].ShouldBe("login");
        var record = fixture.Logger.Records.ShouldHaveSingleItem();
        record.EventId.Id.ShouldBe(AspNetCoreTelemetry.RateLimitRejectionEventId);
        record.Message.ShouldContain("login", StringComparison.Ordinal);
        fixture.ShouldLeakNothing("203.0.113.9");
    }

    [Fact]
    public async Task Rate_limit_rejection_without_a_named_policy_uses_the_none_policy()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create(rateLimiter: options => options.AddRemoteIpFixedWindowPolicy("login", 1, TimeSpan.FromMinutes(1)));
        var httpContext = fixture.HttpContext();
        var rateLimiterOptions = fixture.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Act
        await rateLimiterOptions.OnRejected.ShouldNotBeNull()(
            new OnRejectedContext { HttpContext = httpContext, Lease = new RejectedRateLimitLease() },
            TestContext.Current.CancellationToken);

        // Assert
        fixture.Meters.Measurements.ShouldHaveSingleItem().Tags[AspNetCoreTelemetry.PolicyTag].ShouldBe(AspNetCoreTelemetry.NoPolicy);
    }
}
