namespace Aspire.Hosting;

/// <summary>
/// Defines default local Grafana LGTM Aspire stack settings.
/// </summary>
public static class GrafanaLgtmStackDefaults
{
    /// <summary>The environment variable that enables the optional local stack.</summary>
    public const string EnableObservabilityStackVariable = "ASPIRE_ENABLE_OBSERVABILITY_STACK";

    /// <summary>Default resource names for the local stack.</summary>
    public static GrafanaLgtmStackResourceNames ResourceNames { get; } = new(
        "opentelemetry-collector",
        "grafana",
        "loki",
        "tempo",
        "prometheus");
}
