using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Proves a faulty logger, meter factory or listener never breaks the host's flow or handlers.</summary>
public sealed class TelemetryFailSafeTests
{
    /// <summary>The faults to simulate: a throwing logger, meter factory or metric listener.</summary>
    public static TheoryData<string> Faults => ["logger", "factory", "listener"];

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
