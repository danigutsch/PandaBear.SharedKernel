using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Proves a faulty logger, meter factory or listener never breaks the host's flow or handlers.</summary>
public sealed class TelemetryFailSafeTests
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

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task Exception_handler_still_answers_with_problem_details_when_reporting_fails(string fault)
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName) { ThrowOnMeasurement = fault == "listener" };
        using var throwingFactory = new ThrowingMeterFactory();
        var reporter = new UnhandledExceptionReporter(
            fault == "logger" ? new ThrowingLogger<UnhandledExceptionReporter>() : new RecordingLogger<UnhandledExceptionReporter>(),
            fault == "factory" ? throwingFactory : meters);
        var handler = new UnhandledExceptionHandler(reporter);
        var services = new ServiceCollection().AddOptions().AddProblemDetails().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();

        // Act
        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("sensitive"), TestContext.Current.CancellationToken);

        // Assert
        handled.ShouldBeTrue("a reporting failure must not turn the 500 response into a framework failure");
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.TryGetProperty("traceId", out _).ShouldBeTrue("the problem body keeps its trace id");
    }

    [Fact]
    public void Public_reporter_never_throws_and_still_counts_when_only_the_logger_fails()
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var reporter = new UnhandledExceptionReporter(new ThrowingLogger<UnhandledExceptionReporter>(), meters);

        // Act
        reporter.Report(new InvalidOperationException(), "/orders/{id}", 500, "trace", handled: true);

        // Assert
        meters.Measurements.ShouldHaveSingleItem("the counter is independent of the logger");
    }

    [Fact]
    public void Reporter_with_a_failing_meter_factory_can_be_constructed_and_still_logs()
    {
        // Arrange
        using var factory = new ThrowingMeterFactory();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();

        // Act
        var reporter = new UnhandledExceptionReporter(logger, factory);
        reporter.Report(new InvalidOperationException(), "/orders/{id}", 500, "trace", handled: true);

        // Assert
        logger.Records.ShouldHaveSingleItem("the log is independent of the counter");
    }
}
