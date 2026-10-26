using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

public sealed class SeaweedFsResourceTests
{
    private const string DataFolder = "/data";

    [Fact]
    public void AddSeaweedFs_adds_a_private_container_with_a_pinned_image_and_a_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Assert
        var image = seaweedFs.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        var endpoints = seaweedFs.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("chrislusf/seaweedfs");
        image.SHA256.ShouldBe("c7d6c721b30ae711db766bbbfd40192776e263d4e51e22f57baef7bef93c12c6");
        endpoints.ShouldHaveCount(2);
        endpoints.ShouldAllSatisfy(endpoint => endpoint.IsExternal.ShouldBeFalse());
        endpoints.ShouldContain(endpoint => endpoint.Name == SeaweedFsResource.S3EndpointName && endpoint.TargetPort == 8333);
        endpoints.ShouldContain(endpoint => endpoint.Name == "master" && endpoint.TargetPort == 9333);
        seaweedFs.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("seaweed_master_/cluster/healthz_200_check");
        seaweedFs.Resource.PrimaryEndpoint.EndpointName.ShouldBe(SeaweedFsResource.S3EndpointName);
    }

    [Fact]
    public async Task AddSeaweedFs_passes_the_credentials_and_the_default_bucket_to_the_container()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");
        var variables = await EnvironmentVariables.Of(seaweedFs.Resource);

        // Assert
        seaweedFs.Resource.AccessKeyParameter.Name.ShouldBe("seaweed-access-key");
        seaweedFs.Resource.SecretKeyParameter.Name.ShouldBe("seaweed-secret-key");
        seaweedFs.Resource.BucketParameter.Name.ShouldBe("seaweed-bucket");
        variables["S3_BUCKET"].ShouldBe("media");
        variables.ContainsKey("AWS_ACCESS_KEY_ID").ShouldBeTrue();
        variables.ContainsKey("AWS_SECRET_ACCESS_KEY").ShouldBeTrue();
    }

    [Fact]
    public void AddSeaweedFs_mounts_no_volume()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Assert
        seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
    }

    [Fact]
    public void WithDataVolume_mounts_a_generated_named_volume_at_the_data_folder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Act
        seaweedFs.WithDataVolume();

        // Assert
        var volume = seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe(VolumeNameGenerator.Generate(seaweedFs, "data"));
        volume.Target.ShouldBe(DataFolder);
        volume.Type.ShouldBe(ContainerMountType.Volume);
        volume.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void WithDataVolume_uses_the_given_name_and_read_only_flag()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Act
        seaweedFs.WithDataVolume("seaweedfs-data", isReadOnly: true);

        // Assert
        var volume = seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe("seaweedfs-data");
        volume.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void WithDataBindMount_mounts_the_source_at_the_data_folder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");
        var source = Path.Combine(Path.GetTempPath(), "seaweedfs-data");

        // Act
        seaweedFs.WithDataBindMount(source);

        // Assert
        var mount = seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        mount.Source.ShouldBe(source);
        mount.Target.ShouldBe(DataFolder);
        mount.Type.ShouldBe(ContainerMountType.BindMount);
    }

    [Fact]
    public void WithHostPort_sets_the_host_port_of_the_s3_endpoint()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Act
        seaweedFs.WithHostPort(18333);

        // Assert
        var s3 = seaweedFs.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem(endpoint => endpoint.Name == SeaweedFsResource.S3EndpointName);
        s3.Port.ShouldBe(18333);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WithDataBindMount_rejects_a_blank_source(string source)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Act
        var action = () => seaweedFs.WithDataBindMount(source);

        // Assert
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("source");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddSeaweedFs_rejects_a_blank_default_bucket(string bucketDefault)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var action = () => builder.AddSeaweedFs("seaweed", bucketDefault);

        // Assert
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("bucketDefault");
    }
}
