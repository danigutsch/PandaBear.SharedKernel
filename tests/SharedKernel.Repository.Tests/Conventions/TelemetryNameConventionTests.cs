namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Guards the prefixes that <c>AddSharedKernelInstrumentation</c> registers: every meter and activity source name a library declares
/// starts with <c>PandaBear.</c> or <c>SharedKernel.</c>, so a host that calls it exports the telemetry of every package it uses.
/// </summary>
public sealed class TelemetryNameConventionTests
{
    [Fact]
    public void Every_declared_meter_and_activity_source_name_has_a_registered_prefix()
    {
        // Arrange
        var sourceRoot = Path.Combine(SharedKernelPackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel");

        // Act
        var names = TelemetryNameScanner.DeclaredTelemetryNames(sourceRoot);

        // Assert
        names.Count.ShouldBeGreaterThanOrEqualTo(8, "the scan must find the telemetry names it guards");
        names.Where(entry => !entry.Name.StartsWith("PandaBear.", StringComparison.Ordinal)
                && !entry.Name.StartsWith("SharedKernel.", StringComparison.Ordinal))
            .Select(entry => $"{entry.File}: {entry.Name}")
            .ShouldBeEmpty();
    }
}
