using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.RateLimiting.Tests;

/// <summary>Proves a faulty logger, meter factory or listener never breaks the host's rate-limit rejection callback.</summary>
public sealed class RateLimitTelemetryFailSafeTests
{
    /// <summary>The faults to simulate: a throwing logger, meter factory or metric listener.</summary>
    public static TheoryData<string> Faults => ["logger", "factory", "listener"];

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task Rate_limit_report_failure_does_not_stop_the_hosts_own_callback(string fault)
    {
        // Arrange
        var ran = false;
        using var fixture = SecurityEventTestFixture.Create(
            rateLimiter: options => options.OnRejected = (_, _) =>
            {
                ran = true;
                return ValueTask.CompletedTask;
            },
            logger: fault == "logger" ? new ThrowingLogger<SecurityEventReporter>() : null,
            meterFactory: fault == "factory" ? new ThrowingMeterFactory() : null);
        fixture.Meters.ThrowOnMeasurement = fault == "listener";
        var rejected = fixture.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.OnRejected.ShouldNotBeNull();

        // Act
        await rejected(new OnRejectedContext { HttpContext = fixture.HttpContext(), Lease = new RejectedRateLimitLease() }, TestContext.Current.CancellationToken);

        // Assert
        ran.ShouldBeTrue("the host's own callback must run even when telemetry fails");
    }
}
