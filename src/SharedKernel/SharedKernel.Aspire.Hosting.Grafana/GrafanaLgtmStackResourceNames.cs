namespace Aspire.Hosting;

/// <summary>
/// Contains the Aspire resource names used by a local Grafana LGTM stack.
/// </summary>
/// <param name="OpenTelemetryCollector">The OpenTelemetry Collector resource name.</param>
/// <param name="Grafana">The Grafana resource name.</param>
/// <param name="Loki">The Loki resource name.</param>
/// <param name="Tempo">The Tempo resource name.</param>
/// <param name="Prometheus">The Prometheus resource name.</param>
public sealed record GrafanaLgtmStackResourceNames(
    string OpenTelemetryCollector,
    string Grafana,
    string Loki,
    string Tempo,
    string Prometheus);
