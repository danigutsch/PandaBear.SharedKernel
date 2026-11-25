using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace SharedKernel.Observability.Tests;

/// <summary>A metric exporter that drops every batch, so a meter provider has a reader and enables the instruments it selects.</summary>
internal sealed class DiscardingMetricExporter : BaseExporter<Metric>
{
    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
}
