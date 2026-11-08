using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace PandaBear.AspNetCore.Authentication.Tests;

/// <summary>Proves a faulty logger, meter factory or listener never breaks the host's JWT bearer handlers.</summary>
public sealed class AuthenticationTelemetryFailSafeTests
{
    /// <summary>The faults to simulate: a throwing logger, meter factory or metric listener.</summary>
    public static TheoryData<string> Faults => ["logger", "factory", "listener"];

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task Forbidden_report_failure_does_not_stop_the_hosts_own_handler(string fault)
    {
        // Arrange
        var ran = false;
        using var fixture = SecurityEventTestFixture.Create(
            jwt => jwt.Events.OnForbidden = _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            logger: fault == "logger" ? new ThrowingLogger<SecurityEventReporter>() : null,
            meterFactory: fault == "factory" ? new ThrowingMeterFactory() : null);
        fixture.Meters.ThrowOnMeasurement = fault == "listener";
        var options = fixture.JwtOptions();

        // Act
        await options.Events.Forbidden(new ForbiddenContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options));

        // Assert
        ran.ShouldBeTrue("the host's own handler must run even when telemetry fails");
    }

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task Authentication_failed_and_challenge_report_failures_do_not_stop_the_hosts_own_handlers(string fault)
    {
        // Arrange
        var ran = new List<string>();
        using var fixture = SecurityEventTestFixture.Create(
            jwt =>
            {
                jwt.Events.OnAuthenticationFailed = _ =>
                {
                    ran.Add("failed");
                    return Task.CompletedTask;
                };
                jwt.Events.OnChallenge = _ =>
                {
                    ran.Add("challenge");
                    return Task.CompletedTask;
                };
            },
            logger: fault == "logger" ? new ThrowingLogger<SecurityEventReporter>() : null,
            meterFactory: fault == "factory" ? new ThrowingMeterFactory() : null);
        fixture.Meters.ThrowOnMeasurement = fault == "listener";
        var options = fixture.JwtOptions();

        // Act
        await options.Events.AuthenticationFailed(new AuthenticationFailedContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options)
        {
            Exception = new InvalidOperationException()
        });
        await options.Events.Challenge(new JwtBearerChallengeContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options, new()));

        // Assert
        ran.ShouldBeEquivalentTo("failed", "challenge");
    }
}
