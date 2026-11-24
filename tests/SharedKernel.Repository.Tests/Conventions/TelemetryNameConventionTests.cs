using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Guards the telemetry standard in <c>docs/telemetry.md</c>: meters and activity sources are named after the package that creates
/// them, so the <c>SharedKernel.*</c> pattern that <c>AddInstrumentation</c> registers exports every package; instrument names start
/// with <c>sharedkernel.</c> and carry a description; durations are seconds with the shared bucket advice; and attribute keys are
/// semantic-convention names or start with <c>sharedkernel.</c>.
/// </summary>
public sealed partial class TelemetryNameConventionTests
{
    private static string SourceRoot => Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel");

    [Fact]
    public void Every_declared_meter_and_activity_source_name_has_the_registered_prefix()
    {
        // Act
        var names = TelemetryNameScanner.DeclaredTelemetryNames(SourceRoot);

        // Assert
        names.Count.ShouldBeGreaterThanOrEqualTo(8, "the scan must find the telemetry names it guards");
        names.Where(static entry => !entry.Name.StartsWith("SharedKernel.", StringComparison.Ordinal))
            .Select(static entry => $"{entry.File}: {entry.Name}")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Every_meter_and_activity_source_is_named_after_the_package_that_creates_it()
    {
        // Arrange
        var scanner = new TelemetrySourceScanner(SourceRoot);

        // Act
        var created = scanner.CreatedTelemetryNames();

        // Assert
        created.Count.ShouldBeGreaterThanOrEqualTo(14, "the scan must find the meters and sources it guards");
        created.Where(static entry => !string.Equals(entry.Name, entry.Package, StringComparison.Ordinal))
            .Select(static entry => $"{entry.File}: {entry.Name ?? "<unresolved>"} is not {entry.Package}")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Every_instrument_name_starts_with_sharedkernel_and_has_a_description()
    {
        // Arrange
        var scanner = new TelemetrySourceScanner(SourceRoot);

        // Act
        var instruments = scanner.Instruments();

        // Assert
        instruments.Count.ShouldBeGreaterThanOrEqualTo(24, "the scan must find the instruments it guards");
        instruments.Where(static instrument => instrument.Name is null
                || !SharedKernelNameRegex().IsMatch(instrument.Name)
                || instrument.Name.EndsWith("_total", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(instrument.Description))
            .Select(static instrument => $"{instrument.File}: {instrument.Name ?? "<unresolved>"} (description: {instrument.Description ?? "none"})")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Every_duration_histogram_records_seconds_with_the_shared_bucket_advice()
    {
        // Arrange
        var scanner = new TelemetrySourceScanner(SourceRoot);

        // Act
        var durations = scanner.Instruments()
            .Where(static instrument => string.Equals(instrument.Kind, "Histogram", StringComparison.Ordinal)
                && (instrument.Name?.EndsWith(".duration", StringComparison.Ordinal) == true || instrument.Unit is "s" or "ms" or "us" or "ns" or "min" or "h"))
            .ToList();
        var milliseconds = scanner.MillisecondRecordings();

        // Assert
        durations.Count.ShouldBeGreaterThanOrEqualTo(7, "the scan must find the duration histograms it guards");
        durations.Where(static instrument => instrument.Unit != "s" || instrument.Advice != "DurationBuckets.Advice")
            .Select(static instrument => $"{instrument.File}: {instrument.Name} (unit: {instrument.Unit ?? "none"}, advice: {instrument.Advice ?? "none"})")
            .ShouldBeEmpty();
        milliseconds.ShouldBeEmpty("durations are recorded in seconds");
    }

    [Fact]
    public void Every_attribute_key_is_a_semantic_convention_name_or_starts_with_sharedkernel()
    {
        // Arrange
        var scanner = new TelemetrySourceScanner(SourceRoot);

        // Act
        var keys = scanner.AttributeKeys();

        // Assert
        keys.Select(static entry => entry.Key).Distinct(StringComparer.Ordinal).Count()
            .ShouldBeGreaterThanOrEqualTo(40, "the scan must find the attribute keys it guards");
        keys.Where(static entry => !TelemetrySourceScanner.SemanticConventionAttributes.Contains(entry.Key)
                && !SharedKernelNameRegex().IsMatch(entry.Key))
            .Select(static entry => $"{entry.File}: {entry.Key}")
            .Distinct(StringComparer.Ordinal)
            .ShouldBeEmpty();
    }

    [GeneratedRegex("""^sharedkernel(?:\.[a-z0-9]+(?:_[a-z0-9]+)*)+$""")]
    private static partial Regex SharedKernelNameRegex();
}
