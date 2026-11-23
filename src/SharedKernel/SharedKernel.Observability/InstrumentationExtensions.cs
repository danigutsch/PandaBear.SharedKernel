using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PandaBear.Observability;

/// <summary>
/// Registers the meters and activity sources of every SharedKernel package by name prefix. Each package names its telemetry
/// <c>PandaBear.*</c> or <c>SharedKernel.*</c> (a repository convention test enforces it), so one call covers the packages a host uses
/// without naming them and without referencing the packages it does not use. A package that is not loaded creates no meter or source, so
/// nothing extra is exported.
/// </summary>
/// <remarks>
/// <c>AddInstrumentation()</c> takes no type argument, so it never competes with OpenTelemetry's generic <c>AddInstrumentation&lt;T&gt;</c>
/// overloads on the same builders.
/// </remarks>
public static class InstrumentationExtensions
{
    /// <summary>Gets the name patterns of SharedKernel meters and activity sources: <c>PandaBear.*</c> and <c>SharedKernel.*</c>.</summary>
    public static IReadOnlyList<string> NamePatterns { get; } = ["PandaBear.*", "SharedKernel.*"];

    /// <summary>Collects the metrics of every SharedKernel package.</summary>
    /// <param name="metrics">The meter provider builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="metrics"/> is <see langword="null"/>.</exception>
    public static MeterProviderBuilder AddInstrumentation(this MeterProviderBuilder metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.AddMeter([.. NamePatterns]);
    }

    /// <summary>Traces the activities of every SharedKernel package.</summary>
    /// <param name="tracing">The tracer provider builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tracing"/> is <see langword="null"/>.</exception>
    public static TracerProviderBuilder AddInstrumentation(this TracerProviderBuilder tracing)
    {
        ArgumentNullException.ThrowIfNull(tracing);

        return tracing.AddSource([.. NamePatterns]);
    }
}
