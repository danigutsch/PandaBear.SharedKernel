using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the individual Grafana, Loki, Prometheus and Tempo container resource builders.</summary>
public sealed class GrafanaLgtmStackResourceTests
{
    private static readonly string ConfigurationRoot = Path.Combine(Path.GetTempPath(), "grafana-lgtm-test");

    [Fact]
    public void AddGrafana_adds_a_container_with_a_pinned_image_and_a_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var grafana = builder.AddGrafana("grafana", ConfigurationRoot);

        // Assert
        var image = grafana.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var endpoint = grafana.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("grafana/grafana");
        image.SHA256.ShouldBe("b5b59bfc7561634c2d7b136c4543d702ebcc94a3da477f21ff26f89ffd4214fa");
        endpoint.TargetPort.ShouldBe(3000);
        grafana.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("grafana_http_/api/health_200_check");
        grafana.Resource.PrimaryEndpoint.EndpointName.ShouldBe("http");
    }

    [Fact]
    public void AddLoki_adds_a_container_with_a_pinned_image_and_a_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var loki = builder.AddLoki("loki", Path.Combine(ConfigurationRoot, "loki.yaml"));

        // Assert
        var image = loki.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var endpoint = loki.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("grafana/loki");
        image.SHA256.ShouldBe("a74594532eec4cc313401beedc4dd2708c43674c032084b1aeb87c14a5be1745");
        endpoint.TargetPort.ShouldBe(3100);
        loki.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("loki_http_/ready_200_check");
        loki.Resource.PrimaryEndpoint.EndpointName.ShouldBe("http");
    }

    [Fact]
    public void AddPrometheus_adds_a_container_with_a_pinned_image_and_a_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var prometheus = builder.AddPrometheus("prometheus", Path.Combine(ConfigurationRoot, "prometheus.yml"));

        // Assert
        var image = prometheus.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var endpoint = prometheus.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("prom/prometheus");
        image.SHA256.ShouldBe("63805ebb8d2b3920190daf1cb14a60871b16fd38bed42b857a3182bc621f4996");
        endpoint.TargetPort.ShouldBe(9090);
        prometheus.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("prometheus_http_/-/ready_200_check");
        prometheus.Resource.PrimaryEndpoint.EndpointName.ShouldBe("http");
    }

    [Fact]
    public void AddTempo_adds_a_container_with_a_pinned_image_and_an_otlp_grpc_endpoint()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var tempo = builder.AddTempo("tempo", Path.Combine(ConfigurationRoot, "tempo.yaml"));

        // Assert
        var image = tempo.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var endpoint = tempo.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem(annotation => annotation.Name == "otlp-grpc");
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("grafana/tempo");
        image.SHA256.ShouldBe("bc9245fe3da4e63dc4c6862d9c2dad9bcd8be13d0ba4f7705fa6acda4c904d0e");
        endpoint.Name.ShouldBe("otlp-grpc");
        endpoint.TargetPort.ShouldBe(4317);
        tempo.Resource.PrimaryEndpoint.EndpointName.ShouldBe("otlp-grpc");
    }

    [Fact]
    public async Task AddGrafana_enables_anonymous_local_access()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var grafana = builder.AddGrafana("grafana", ConfigurationRoot);

        // Act
        var environmentVariables = await EnvironmentVariables.Of(grafana.Resource);

        // Assert
        environmentVariables["GF_AUTH_ANONYMOUS_ENABLED"].ShouldBe("true");
    }

    [Fact]
    public void WithHostPort_sets_the_host_port_of_the_primary_endpoint()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var grafana = builder.AddGrafana("grafana", ConfigurationRoot);
        var loki = builder.AddLoki("loki", Path.Combine(ConfigurationRoot, "loki.yaml"));
        var tempo = builder.AddTempo("tempo", Path.Combine(ConfigurationRoot, "tempo.yaml"));
        var prometheus = builder.AddPrometheus("prometheus", Path.Combine(ConfigurationRoot, "prometheus.yml"));

        // Act
        grafana.WithHostPort(13000);
        loki.WithHostPort(13100);
        tempo.WithHostPort(14317);
        prometheus.WithHostPort(19090);

        // Assert
        grafana.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem().Port.ShouldBe(13000);
        loki.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem().Port.ShouldBe(13100);
        tempo.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem(annotation => annotation.Name == "otlp-grpc").Port.ShouldBe(14317);
        prometheus.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem().Port.ShouldBe(19090);
    }

    [Fact]
    public void AddTempo_adds_a_readiness_check_on_the_http_port()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var tempo = builder.AddTempo("tempo", Path.Combine(ConfigurationRoot, "tempo.yaml"));

        // Assert
        var http = tempo.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem(annotation => annotation.Name == "http");
        http.TargetPort.ShouldBe(3200);
        http.IsExternal.ShouldBeFalse();
        tempo.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("tempo_http_/ready_200_check");
    }

    [Fact]
    public void WithDataVolume_mounts_a_generated_named_volume_at_the_data_folder_of_each_resource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var grafana = builder.AddGrafana("grafana", ConfigurationRoot);
        var loki = builder.AddLoki("loki", Path.Combine(ConfigurationRoot, "loki.yaml"));
        var tempo = builder.AddTempo("tempo", Path.Combine(ConfigurationRoot, "tempo.yaml"));
        var prometheus = builder.AddPrometheus("prometheus", Path.Combine(ConfigurationRoot, "prometheus.yml"));

        // Act
        grafana.WithDataVolume();
        loki.WithDataVolume();
        tempo.WithDataVolume();
        prometheus.WithDataVolume("prometheus-data");

        // Assert
        var grafanaVolume = MountAnnotations.Volume(grafana.Resource);
        grafanaVolume.Target.ShouldBe("/var/lib/grafana");
        grafanaVolume.Source.ShouldBe(VolumeNameGenerator.Generate(grafana, "data"));
        MountAnnotations.Volume(loki.Resource).Target.ShouldBe("/loki");
        MountAnnotations.Volume(tempo.Resource).Target.ShouldBe("/var/tempo");
        var prometheusVolume = MountAnnotations.Volume(prometheus.Resource);
        prometheusVolume.Target.ShouldBe("/prometheus");
        prometheusVolume.Source.ShouldBe("prometheus-data");
    }

    [Fact]
    public void WithDataBindMount_mounts_the_source_at_the_data_folder_of_each_resource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var grafana = builder.AddGrafana("grafana", ConfigurationRoot);
        var loki = builder.AddLoki("loki", Path.Combine(ConfigurationRoot, "loki.yaml"));
        var tempo = builder.AddTempo("tempo", Path.Combine(ConfigurationRoot, "tempo.yaml"));
        var prometheus = builder.AddPrometheus("prometheus", Path.Combine(ConfigurationRoot, "prometheus.yml"));
        var source = Path.Combine(Path.GetTempPath(), "lgtm-data");

        // Act
        grafana.WithDataBindMount(source);
        loki.WithDataBindMount(source);
        tempo.WithDataBindMount(source);
        prometheus.WithDataBindMount(source);

        // Assert
        MountAnnotations.Mount(grafana.Resource, "/var/lib/grafana").Type.ShouldBe(ContainerMountType.BindMount);
        MountAnnotations.Mount(loki.Resource, "/loki").Source.ShouldBe(source);
        MountAnnotations.Mount(tempo.Resource, "/var/tempo").Source.ShouldBe(source);
        MountAnnotations.Mount(prometheus.Resource, "/prometheus").Source.ShouldBe(source);
    }
}
