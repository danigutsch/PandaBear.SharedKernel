namespace PandaBear.AspNetCore.Tests;

/// <summary>Proves a telemetry failure is surfaced on standard error; runs alone because it redirects the process-wide stream.</summary>
[Collection(SerialConsoleError.Name)]
public sealed class TelemetryFailureSurfacingTests
{
    [Fact]
    public void Failure_is_surfaced_on_standard_error_without_the_exception_message()
    {
        // Arrange
        using var captured = new ConsoleErrorCapture();
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var reporter = new UnhandledExceptionReporter(new ThrowingLogger<UnhandledExceptionReporter>(), meters);

        // Act
        reporter.Report(new InvalidOperationException(), "/orders/{id}", 500, "trace", handled: true);

        // Assert
        captured.Text.ShouldContain("InvalidOperationException", "the failure type is surfaced");
        captured.Text.ShouldNotContain(ThrowingLogger<UnhandledExceptionReporter>.Secret, "the failure message must not be surfaced");
    }
}
