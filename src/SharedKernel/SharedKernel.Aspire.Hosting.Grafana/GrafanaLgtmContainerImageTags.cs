namespace Aspire.Hosting;

internal static class GrafanaLgtmContainerImageTags
{
    /// <remarks>docker.io</remarks>
    internal const string Registry = "docker.io";

    /// <remarks>grafana/grafana</remarks>
    internal const string GrafanaImage = "grafana/grafana";

    /// <remarks>12.0.2</remarks>
    internal const string GrafanaTag = "12.0.2";

    /// <summary>The digest of the pinned Grafana image.</summary>
    internal const string GrafanaDigest = "b5b59bfc7561634c2d7b136c4543d702ebcc94a3da477f21ff26f89ffd4214fa";

    /// <remarks>grafana/loki</remarks>
    internal const string LokiImage = "grafana/loki";

    /// <remarks>3.5.1</remarks>
    internal const string LokiTag = "3.5.1";

    /// <summary>The digest of the pinned Loki image.</summary>
    internal const string LokiDigest = "a74594532eec4cc313401beedc4dd2708c43674c032084b1aeb87c14a5be1745";

    /// <remarks>grafana/tempo</remarks>
    internal const string TempoImage = "grafana/tempo";

    /// <remarks>2.8.1</remarks>
    internal const string TempoTag = "2.8.1";

    /// <summary>The digest of the pinned Tempo image.</summary>
    internal const string TempoDigest = "bc9245fe3da4e63dc4c6862d9c2dad9bcd8be13d0ba4f7705fa6acda4c904d0e";

    /// <remarks>prom/prometheus</remarks>
    internal const string PrometheusImage = "prom/prometheus";

    /// <remarks>v3.5.0</remarks>
    internal const string PrometheusTag = "v3.5.0";

    /// <summary>The digest of the pinned Prometheus image.</summary>
    internal const string PrometheusDigest = "63805ebb8d2b3920190daf1cb14a60871b16fd38bed42b857a3182bc621f4996";

    /// <remarks>0.130.1</remarks>
    internal const string OpenTelemetryCollectorTag = "0.130.1";

    /// <summary>The digest of the pinned OpenTelemetry Collector image.</summary>
    internal const string OpenTelemetryCollectorDigest = "9c247564e65ca19f97d891cca19a1a8d291ce631b890885b44e3503c5fdb3895";
}
