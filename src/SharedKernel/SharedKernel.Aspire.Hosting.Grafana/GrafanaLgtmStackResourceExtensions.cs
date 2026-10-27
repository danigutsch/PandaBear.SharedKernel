using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding the Grafana LGTM observability stack to the application model.
/// </summary>
public static class GrafanaLgtmStackResourceExtensions
{
    private const string OtlpExporterEndpointVariableName = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string OtlpExporterProtocolVariableName = "OTEL_EXPORTER_OTLP_PROTOCOL";
    private const string OtlpHttpProtobufProtocol = "http/protobuf";
    private const string OtlpHttpJsonProtocol = "http/json";
    private const string CollectorGrpcEndpointName = "grpc";
    private const string CollectorHttpEndpointName = "http";

    /// <summary>
    /// Adds an OpenTelemetry Collector gateway to the application model and routes the telemetry of compatible resources through it.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="privacyConfigurationFile">The path of the configuration file with the shared receivers and privacy processors.</param>
    /// <param name="routingConfigurationFile">The path of the configuration file with the exporters and pipeline routing.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// Resources that export OTLP telemetry wait until the collector is healthy and send to its gRPC endpoint, or to its HTTP
    /// endpoint when their <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> is <c>http/protobuf</c> or <c>http/json</c>.
    /// The resource is excluded from the manifest, because it is meant for local development.
    /// This version of the package pins the collector container image by digest to its <inheritdoc cref="GrafanaLgtmContainerImageTags.OpenTelemetryCollectorTag"/> tag.
    /// </remarks>
    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollectorGateway(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string privacyConfigurationFile,
        string routingConfigurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(privacyConfigurationFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingConfigurationFile);

        var collector = builder.AddOpenTelemetryCollector(name)
            .WithImageTag(GrafanaLgtmContainerImageTags.OpenTelemetryCollectorTag)
            .WithImageSHA256(GrafanaLgtmContainerImageTags.OpenTelemetryCollectorDigest)
            .WithConfig(Path.GetFullPath(privacyConfigurationFile))
            .WithConfig(Path.GetFullPath(routingConfigurationFile));
        ConfigureAppForwarding(collector);

        return collector.ExcludeFromManifest();
    }

    /// <summary>Points the resource's OTLP exporter endpoint at the collector endpoint that matches its configured protocol, and waits until the collector is healthy.</summary>
    internal static IResourceBuilder<T> ConfigureOpenTelemetryCollectorRouting<T>(
        IResourceBuilder<T> builder,
        IResourceBuilder<OpenTelemetryCollectorResource> collector)
        where T : IResourceWithEnvironment
    {
        builder.WithEnvironment(callback =>
        {
            callback.EnvironmentVariables.TryGetValue(OtlpExporterProtocolVariableName, out var configuredProtocol);
            var endpointName = ResolveCollectorEndpointName(configuredProtocol?.ToString());
            callback.EnvironmentVariables[OtlpExporterEndpointVariableName] = collector.Resource.GetEndpoint(endpointName);
        });
        builder.WithAnnotation(new WaitAnnotation(collector.Resource, WaitType.WaitUntilHealthy));

        return builder;
    }

    private static void ConfigureAppForwarding(IResourceBuilder<OpenTelemetryCollectorResource> collector)
    {
        collector.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((beforeStart, _) =>
        {
            var telemetryResources = beforeStart.Model.Resources
                .OfType<IResourceWithEnvironment>()
                .Where(static resource => resource.HasAnnotationOfType<OtlpExporterAnnotation>());

            foreach (var resource in telemetryResources)
            {
                var resourceBuilder = collector.ApplicationBuilder.CreateResourceBuilder(resource);
                ConfigureOpenTelemetryCollectorRouting(resourceBuilder, collector);
            }

            return Task.CompletedTask;
        });
    }

    private static string ResolveCollectorEndpointName(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol)
            || string.Equals(protocol, CollectorGrpcEndpointName, StringComparison.OrdinalIgnoreCase))
        {
            return CollectorGrpcEndpointName;
        }

        if (string.Equals(protocol, OtlpHttpProtobufProtocol, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocol, OtlpHttpJsonProtocol, StringComparison.OrdinalIgnoreCase))
        {
            return CollectorHttpEndpointName;
        }

        throw new InvalidOperationException($"Unsupported OTLP exporter protocol '{protocol}'.");
    }

    /// <summary>
    /// Adds a Grafana container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configurationRoot">The path of the folder that contains the <c>provisioning</c> and <c>dashboards</c> folders.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This resource includes built-in health checks. When this resource is referenced as a dependency
    /// using the <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until Grafana is able to service requests.
    /// </para>
    /// Anonymous access with the Admin role is enabled and the resource is excluded from the manifest, because it is meant for local development.
    /// This version of the package uses the <inheritdoc cref="GrafanaLgtmContainerImageTags.GrafanaImage"/> container image, pinned by digest to its <inheritdoc cref="GrafanaLgtmContainerImageTags.GrafanaTag"/> tag.
    /// </remarks>
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
            .WithImage(GrafanaLgtmContainerImageTags.GrafanaImage, GrafanaLgtmContainerImageTags.GrafanaTag)
            .WithImageRegistry(GrafanaLgtmContainerImageTags.Registry)
            .WithImageSHA256(GrafanaLgtmContainerImageTags.GrafanaDigest)
            .WithBindMount(Path.Combine(grafanaRoot, "provisioning"), "/etc/grafana/provisioning", isReadOnly: true)
            .WithBindMount(Path.Combine(grafanaRoot, "dashboards"), "/var/lib/grafana/dashboards", isReadOnly: true)
            .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
            .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
            .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
            .WithHttpEndpoint(targetPort: 3000, name: GrafanaResource.HttpEndpointName)
            .WithHttpHealthCheck("/api/health", endpointName: GrafanaResource.HttpEndpointName)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the host port that the Grafana resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaResource> WithHostPort(this IResourceBuilder<GrafanaResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(GrafanaResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a Loki container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configurationFile">The path of the Loki configuration file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This resource includes built-in health checks. When this resource is referenced as a dependency
    /// using the <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until Loki is able to service requests.
    /// </para>
    /// The resource is excluded from the manifest, because it is meant for local development.
    /// This version of the package uses the <inheritdoc cref="GrafanaLgtmContainerImageTags.LokiImage"/> container image, pinned by digest to its <inheritdoc cref="GrafanaLgtmContainerImageTags.LokiTag"/> tag.
    /// </remarks>
    public static IResourceBuilder<LokiResource> AddLoki(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);

        return builder.AddResource(new LokiResource(name))
            .WithImage(GrafanaLgtmContainerImageTags.LokiImage, GrafanaLgtmContainerImageTags.LokiTag)
            .WithImageRegistry(GrafanaLgtmContainerImageTags.Registry)
            .WithImageSHA256(GrafanaLgtmContainerImageTags.LokiDigest)
            .WithBindMount(Path.GetFullPath(configurationFile), "/etc/loki/loki.yaml", isReadOnly: true)
            .WithArgs("-config.file=/etc/loki/loki.yaml")
            .WithHttpEndpoint(targetPort: 3100, name: LokiResource.HttpEndpointName)
            .WithHttpHealthCheck("/ready", endpointName: LokiResource.HttpEndpointName)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the host port that the Loki resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<LokiResource> WithHostPort(this IResourceBuilder<LokiResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(LokiResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a Tempo container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configurationFile">The path of the Tempo configuration file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// The resource is excluded from the manifest, because it is meant for local development.
    /// This version of the package uses the <inheritdoc cref="GrafanaLgtmContainerImageTags.TempoImage"/> container image, pinned by digest to its <inheritdoc cref="GrafanaLgtmContainerImageTags.TempoTag"/> tag.
    /// </remarks>
    public static IResourceBuilder<TempoResource> AddTempo(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);

        return builder.AddResource(new TempoResource(name))
            .WithImage(GrafanaLgtmContainerImageTags.TempoImage, GrafanaLgtmContainerImageTags.TempoTag)
            .WithImageRegistry(GrafanaLgtmContainerImageTags.Registry)
            .WithImageSHA256(GrafanaLgtmContainerImageTags.TempoDigest)
            .WithBindMount(Path.GetFullPath(configurationFile), "/etc/tempo/tempo.yaml", isReadOnly: true)
            .WithArgs("-config.file=/etc/tempo/tempo.yaml")
            .WithEndpoint(targetPort: 4317, name: TempoResource.OtlpGrpcEndpointName)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the host port that the Tempo resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TempoResource> WithHostPort(this IResourceBuilder<TempoResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(TempoResource.OtlpGrpcEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a Prometheus container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configurationFile">The path of the Prometheus configuration file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This resource includes built-in health checks. When this resource is referenced as a dependency
    /// using the <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until Prometheus is able to service requests.
    /// </para>
    /// The resource is excluded from the manifest, because it is meant for local development.
    /// This version of the package uses the <inheritdoc cref="GrafanaLgtmContainerImageTags.PrometheusImage"/> container image, pinned by digest to its <inheritdoc cref="GrafanaLgtmContainerImageTags.PrometheusTag"/> tag.
    /// </remarks>
    public static IResourceBuilder<PrometheusResource> AddPrometheus(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configurationFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);

        return builder.AddResource(new PrometheusResource(name))
            .WithImage(GrafanaLgtmContainerImageTags.PrometheusImage, GrafanaLgtmContainerImageTags.PrometheusTag)
            .WithImageRegistry(GrafanaLgtmContainerImageTags.Registry)
            .WithImageSHA256(GrafanaLgtmContainerImageTags.PrometheusDigest)
            .WithBindMount(Path.GetFullPath(configurationFile), "/etc/prometheus/prometheus.yml", isReadOnly: true)
            .WithArgs("--config.file=/etc/prometheus/prometheus.yml")
            .WithHttpEndpoint(targetPort: 9090, name: PrometheusResource.HttpEndpointName)
            .WithHttpHealthCheck("/-/ready", endpointName: PrometheusResource.HttpEndpointName)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the host port that the Prometheus resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PrometheusResource> WithHostPort(this IResourceBuilder<PrometheusResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(PrometheusResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds Grafana, Loki, Tempo, Prometheus, and an OpenTelemetry Collector gateway to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="configurationRoot">The path of the folder that contains the <c>grafana</c>, <c>loki</c>, <c>otel-collector</c>, <c>prometheus</c>, and <c>tempo</c> configuration folders.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for the Grafana resource.</returns>
    /// <remarks>
    /// The resources are named with <see cref="GrafanaLgtmStackDefaults.ResourceNames"/>.
    /// </remarks>
    public static IResourceBuilder<GrafanaResource> AddGrafanaLgtmStack(
        this IDistributedApplicationBuilder builder,
        string configurationRoot)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationRoot);

        return builder.AddGrafanaLgtmStack(GrafanaLgtmStackDefaults.ResourceNames, configurationRoot);
    }

    /// <summary>
    /// Adds Grafana, Loki, Tempo, Prometheus, and an OpenTelemetry Collector gateway to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="resourceNames">The names of the resources.</param>
    /// <param name="configurationRoot">The path of the folder that contains the <c>grafana</c>, <c>loki</c>, <c>otel-collector</c>, <c>prometheus</c>, and <c>tempo</c> configuration folders.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for the Grafana resource.</returns>
    /// <remarks>
    /// The collector waits for Tempo and Loki, Prometheus waits for the collector, and Grafana waits for Loki, Tempo, and Prometheus.
    /// </remarks>
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

        var collector = builder.AddOpenTelemetryCollectorGateway(
                resourceNames.OpenTelemetryCollector,
                Path.Combine(fullConfigurationRoot, "otel-collector", "privacy.yaml"),
                Path.Combine(fullConfigurationRoot, "otel-collector", "config.yaml"))
            .WaitFor(tempo)
            .WaitFor(loki);

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
