using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds local Grafana LGTM observability resources to an Aspire AppHost model.
/// </summary>
public static class GrafanaLgtmStackResourceExtensions
{
    private const string OtlpGrpcEndpointName = "otlp-grpc";

    /// <summary>Tag for <c>docker.io/otel/opentelemetry-collector-contrib:0.130.1</c>.</summary>
    private const string OpenTelemetryCollectorImageTag = "0.130.1";

    /// <summary>Digest for <c>docker.io/otel/opentelemetry-collector-contrib:0.130.1</c>.</summary>
    private const string OpenTelemetryCollectorImageDigest = "9c247564e65ca19f97d891cca19a1a8d291ce631b890885b44e3503c5fdb3895";

    /// <summary>Tag for <c>docker.io/grafana/grafana:12.0.2</c>.</summary>
    private const string GrafanaImageTag = "12.0.2";

    /// <summary>Digest for <c>docker.io/grafana/grafana:12.0.2</c>.</summary>
    private const string GrafanaImageDigest = "b5b59bfc7561634c2d7b136c4543d702ebcc94a3da477f21ff26f89ffd4214fa";

    /// <summary>Tag for <c>docker.io/grafana/loki:3.5.1</c>.</summary>
    private const string LokiImageTag = "3.5.1";

    /// <summary>Digest for <c>docker.io/grafana/loki:3.5.1</c>.</summary>
    private const string LokiImageDigest = "a74594532eec4cc313401beedc4dd2708c43674c032084b1aeb87c14a5be1745";

    /// <summary>Tag for <c>docker.io/grafana/tempo:2.8.1</c>.</summary>
    private const string TempoImageTag = "2.8.1";

    /// <summary>Digest for <c>docker.io/grafana/tempo:2.8.1</c>.</summary>
    private const string TempoImageDigest = "bc9245fe3da4e63dc4c6862d9c2dad9bcd8be13d0ba4f7705fa6acda4c904d0e";

    /// <summary>Tag for <c>docker.io/prom/prometheus:v3.5.0</c>.</summary>
    private const string PrometheusImageTag = "v3.5.0";

    /// <summary>Digest for <c>docker.io/prom/prometheus:v3.5.0</c>.</summary>
    private const string PrometheusImageDigest = "63805ebb8d2b3920190daf1cb14a60871b16fd38bed42b857a3182bc621f4996";

    /// <summary>
    /// Adds a Grafana resource to the Aspire model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="configurationRoot">The root folder containing Grafana provisioning and dashboard folders.</param>
    /// <returns>The configured Grafana resource.</returns>
    public static IResourceBuilder<GrafanaResource> AddGrafana(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationRoot)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationRoot);

        var grafanaRoot = Path.GetFullPath(configurationRoot);

        return builder.AddResource(new GrafanaResource(name))
            .WithImage("grafana/grafana")
            .WithImageTag(GrafanaImageTag)
            .WithImageSHA256(GrafanaImageDigest)
            .WithBindMount(Path.Combine(grafanaRoot, "provisioning"), "/etc/grafana/provisioning", isReadOnly: true)
            .WithBindMount(Path.Combine(grafanaRoot, "dashboards"), "/var/lib/grafana/dashboards", isReadOnly: true)
            .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
            .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
            .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
            .WithHttpEndpoint(targetPort: 3000)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds a Loki resource to the Aspire model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="configurationFile">The Loki configuration file.</param>
    /// <returns>The configured Loki resource.</returns>
    public static IResourceBuilder<LokiResource> AddLoki(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);

        return builder.AddResource(new LokiResource(name))
            .WithImage("grafana/loki")
            .WithImageTag(LokiImageTag)
            .WithImageSHA256(LokiImageDigest)
            .WithBindMount(Path.GetFullPath(configurationFile), "/etc/loki/loki.yaml", isReadOnly: true)
            .WithArgs("-config.file=/etc/loki/loki.yaml")
            .WithHttpEndpoint(targetPort: 3100)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds a Tempo resource to the Aspire model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="configurationFile">The Tempo configuration file.</param>
    /// <returns>The configured Tempo resource.</returns>
    public static IResourceBuilder<TempoResource> AddTempo(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);

        return builder.AddResource(new TempoResource(name))
            .WithImage("grafana/tempo")
            .WithImageTag(TempoImageTag)
            .WithImageSHA256(TempoImageDigest)
            .WithBindMount(Path.GetFullPath(configurationFile), "/etc/tempo/tempo.yaml", isReadOnly: true)
            .WithArgs("-config.file=/etc/tempo/tempo.yaml")
            .WithEndpoint(targetPort: 4317, name: OtlpGrpcEndpointName)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds a Prometheus resource to the Aspire model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="configurationFile">The Prometheus configuration file.</param>
    /// <returns>The configured Prometheus resource.</returns>
    public static IResourceBuilder<PrometheusResource> AddPrometheus(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);

        return builder.AddResource(new PrometheusResource(name))
            .WithImage("prom/prometheus")
            .WithImageTag(PrometheusImageTag)
            .WithImageSHA256(PrometheusImageDigest)
            .WithBindMount(Path.GetFullPath(configurationFile), "/etc/prometheus/prometheus.yml", isReadOnly: true)
            .WithArgs("--config.file=/etc/prometheus/prometheus.yml")
            .WithHttpEndpoint(targetPort: 9090)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds Grafana, Loki, Tempo, Prometheus, and an OpenTelemetry Collector to the Aspire model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="configurationRoot">The root folder containing <c>grafana</c>, <c>loki</c>, <c>otel-collector</c>, <c>prometheus</c>, and <c>tempo</c> configuration folders.</param>
    /// <returns>The configured Grafana resource.</returns>
    public static IResourceBuilder<GrafanaResource> AddGrafanaLgtmStack(
        this IDistributedApplicationBuilder builder,
        string configurationRoot)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationRoot);

        return builder.AddGrafanaLgtmStack(GrafanaLgtmStackDefaults.ResourceNames, configurationRoot);
    }

    /// <summary>
    /// Adds Grafana, Loki, Tempo, Prometheus, and an OpenTelemetry Collector to the Aspire model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="resourceNames">The resource names to use in the Aspire model.</param>
    /// <param name="configurationRoot">The root folder containing <c>grafana</c>, <c>loki</c>, <c>otel-collector</c>, <c>prometheus</c>, and <c>tempo</c> configuration folders.</param>
    /// <returns>The configured Grafana resource.</returns>
    public static IResourceBuilder<GrafanaResource> AddGrafanaLgtmStack(
        this IDistributedApplicationBuilder builder,
        GrafanaLgtmStackResourceNames resourceNames,
        string configurationRoot)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resourceNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationRoot);

        var fullConfigurationRoot = Path.GetFullPath(configurationRoot);
        var tempo = builder.AddTempo(resourceNames.Tempo, Path.Combine(fullConfigurationRoot, "tempo", "tempo.yaml"));
        var loki = builder.AddLoki(resourceNames.Loki, Path.Combine(fullConfigurationRoot, "loki", "loki.yaml"));

        var collector = builder.AddOpenTelemetryCollector(resourceNames.OpenTelemetryCollector)
            .WithImageTag(OpenTelemetryCollectorImageTag)
            .WithImageSHA256(OpenTelemetryCollectorImageDigest)
            .WithConfig(Path.Combine(fullConfigurationRoot, "otel-collector", "config.yaml"))
            .WithAppForwarding()
            .WaitFor(tempo)
            .WaitFor(loki)
            .ExcludeFromManifest();

        var prometheus = builder.AddPrometheus(
            resourceNames.Prometheus,
            Path.Combine(fullConfigurationRoot, "prometheus", "prometheus.yml"))
            .WaitFor(collector);

        return builder.AddGrafana(resourceNames.Grafana, Path.Combine(fullConfigurationRoot, "grafana"))
            .WaitFor(loki)
            .WaitFor(tempo)
            .WaitFor(prometheus);
    }
}
