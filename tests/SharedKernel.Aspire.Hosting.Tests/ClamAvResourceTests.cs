using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

public sealed class ClamAvResourceTests
{
    private const string DefinitionsFolder = "/var/lib/clamav";

    [Fact]
    public void AddClamAv_adds_a_private_container_with_a_pinned_image_and_a_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var clamAv = builder.AddClamAv("clamav");

        // Assert
        var image = clamAv.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var endpoint = clamAv.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        var healthCheck = clamAv.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem();
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("clamav/clamav");
        image.SHA256.ShouldBe("6f4a9e7d616ffc8d1070200fe35ac860735fdd522161a1043f94856e6ee13c28");
        endpoint.Name.ShouldBe(ClamAvResource.TcpEndpointName);
        endpoint.TargetPort.ShouldBe(3310);
        endpoint.IsExternal.ShouldBeFalse();
        healthCheck.Key.ShouldBe("clamav-tcp-ping");
        clamAv.Resource.PrimaryEndpoint.EndpointName.ShouldBe(ClamAvResource.TcpEndpointName);
    }

    [Fact]
    public void AddClamAv_mounts_no_volume()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var clamAv = builder.AddClamAv("clamav");

        // Assert
        clamAv.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
    }

    [Fact]
    public void WithDataVolume_mounts_a_generated_named_volume_at_the_definitions_folder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var clamAv = builder.AddClamAv("clamav");

        // Act
        clamAv.WithDataVolume();

        // Assert
        var volume = clamAv.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe(VolumeNameGenerator.Generate(clamAv, "data"));
        volume.Target.ShouldBe(DefinitionsFolder);
        volume.Type.ShouldBe(ContainerMountType.Volume);
        volume.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void WithDataVolume_uses_the_given_name_and_read_only_flag()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var clamAv = builder.AddClamAv("clamav");

        // Act
        clamAv.WithDataVolume("clamav-definitions", isReadOnly: true);

        // Assert
        var volume = clamAv.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe("clamav-definitions");
        volume.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void WithDataBindMount_mounts_the_source_at_the_definitions_folder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var clamAv = builder.AddClamAv("clamav");
        var source = Path.Combine(Path.GetTempPath(), "clamav-definitions");

        // Act
        clamAv.WithDataBindMount(source);

        // Assert
        var mount = clamAv.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        mount.Source.ShouldBe(source);
        mount.Target.ShouldBe(DefinitionsFolder);
        mount.Type.ShouldBe(ContainerMountType.BindMount);
    }

    [Fact]
    public void WithHostPort_sets_the_host_port_of_the_tcp_endpoint()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var clamAv = builder.AddClamAv("clamav");

        // Act
        clamAv.WithHostPort(13310);

        // Assert
        var endpoint = clamAv.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        endpoint.Port.ShouldBe(13310);
    }

    [Fact]
    public async Task WithFreshClam_false_disables_the_update_daemon()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var enabled = builder.AddClamAv("enabled").WithFreshClam(true);
        var disabled = builder.AddClamAv("disabled").WithFreshClam(false);

        // Act
        var enabledVariables = await EnvironmentVariables.Of(enabled.Resource);
        var disabledVariables = await EnvironmentVariables.Of(disabled.Resource);

        // Assert
        enabledVariables.ContainsKey("CLAMAV_NO_FRESHCLAMD").ShouldBeFalse();
        disabledVariables["CLAMAV_NO_FRESHCLAMD"].ShouldBe("true");
    }

    [Fact]
    public async Task WithClamAvReference_injects_the_host_and_port_of_the_daemon()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var clamAv = builder.AddClamAv("clamav");
        var endpoint = clamAv.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "clamav", 3310, "3310");

        // Act
        var consumer = builder.AddResource(new ContainerResource("consumer")).WithClamAvReference(clamAv);
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        variables["MalwareScanning__ClamAv__Host"].ShouldBe("clamav");
        variables["MalwareScanning__ClamAv__Port"].ShouldBe("3310");
    }
}
