using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the composed Grafana LGTM observability stack: default names, resource registration and OTLP routing through the collector gateway.</summary>
public sealed class GrafanaLgtmStackTests
{
    private static readonly string ConfigurationRoot = Path.Combine(Path.GetTempPath(), "grafana-lgtm-test");

    [Fact]
    public void Default_resource_names_match_the_documented_local_stack()
    {
        // Arrange
        var names = GrafanaLgtmStackDefaults.ResourceNames;

        // Act
        var configuredNames = new[]
        {
            names.OpenTelemetryCollector,
            names.Grafana,
            names.Loki,
            names.Tempo,
            names.Prometheus,
        };

        // Assert
        GrafanaLgtmStackDefaults.EnableObservabilityStackVariable.ShouldBe("ASPIRE_ENABLE_OBSERVABILITY_STACK");
        configuredNames.ShouldMatchCollection(
            name => name.ShouldBe("opentelemetry-collector"),
            name => name.ShouldBe("grafana"),
            name => name.ShouldBe("loki"),
            name => name.ShouldBe("tempo"),
            name => name.ShouldBe("prometheus"));
    }

    [Fact]
    public void AddGrafanaLgtmStack_adds_the_expected_resources()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var resourceNames = new GrafanaLgtmStackResourceNames("collector-test", "grafana-test", "loki-test", "tempo-test", "prometheus-test");

        // Act
        var grafana = builder.AddGrafanaLgtmStack(resourceNames, ConfigurationRoot);
        var resources = builder.Resources.ToArray();

        // Assert
        grafana.Resource.ShouldBeOfType<GrafanaResource>();
        resources.ShouldContain(resource => resource is GrafanaResource && resource.Name == "grafana-test");
        resources.ShouldContain(resource => resource is LokiResource && resource.Name == "loki-test");
        resources.ShouldContain(resource => resource is TempoResource && resource.Name == "tempo-test");
        resources.ShouldContain(resource => resource is PrometheusResource && resource.Name == "prometheus-test");
        resources.ShouldContain(resource => resource.Name == "collector-test");
    }

    [Fact]
    public async Task AddOpenTelemetryCollectorGateway_routes_http_protobuf_to_the_http_endpoint_and_waits_for_health()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish"]);
        var collector = builder.AddOpenTelemetryCollectorGateway(
            "collector-test",
            Path.Combine(ConfigurationRoot, "privacy.yaml"),
            Path.Combine(ConfigurationRoot, "aspire.yaml"));
        var sender = builder.AddContainer("sender-test", "test-image")
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
        sender.WithOtlpExporterIfEndpointAvailable(OtlpProtocol.HttpProtobuf);
        using var application = builder.Build();
        await builder.Eventing.PublishAsync(
            new BeforeStartEvent(application.Services, application.Services.GetRequiredService<DistributedApplicationModel>()),
            TestContext.Current.CancellationToken);
        var environmentVariables = new Dictionary<string, object>();
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);

        // Act
        foreach (var annotation in sender.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(new EnvironmentCallbackContext(executionContext, sender.Resource, environmentVariables, TestContext.Current.CancellationToken));
        }

        var endpoint = environmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"].ShouldBeOfType<EndpointReference>();
        var wait = sender.Resource.Annotations.OfType<WaitAnnotation>().ShouldHaveSingleItem();

        // Assert
        environmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"].ShouldBe("http/protobuf");
        endpoint.Resource.ShouldBeSameAs(collector.Resource);
        endpoint.EndpointName.ShouldBe("http");
        wait.Resource.ShouldBeSameAs(collector.Resource);
        wait.WaitType.ShouldBe(WaitType.WaitUntilHealthy);
    }
}
