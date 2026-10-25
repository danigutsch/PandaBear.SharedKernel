using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

public sealed class SeaweedFsVolumeTests
{
    private const string ResourceNameSuffixConfigurationKey = "DcpPublisher:ResourceNameSuffix";

    [Fact]
    public void Data_uses_a_named_volume_when_no_resource_name_suffix_is_set()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Assert
        var volume = seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe("seaweed-data", "an AppHost run keeps the stored objects for the next run");
        volume.Target.ShouldBe("/data");
        volume.Type.ShouldBe(ContainerMountType.Volume);
        volume.IsReadOnly.ShouldBeFalse("SeaweedFS writes the stored objects");
    }

    [Theory]
    [InlineData("test")]
    [InlineData("hosted-5a1f4f87055d40469e33670147b3acdd")]
    public void Data_uses_an_anonymous_volume_when_a_resource_name_suffix_is_set(string suffix)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration[ResourceNameSuffixConfigurationKey] = suffix;

        // Act
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Assert
        var volume = seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBeNull("a named volume of an isolated instance would outlive its container and never be reused");
        volume.Target.ShouldBe("/data");
        volume.Type.ShouldBe(ContainerMountType.Volume);
        volume.IsReadOnly.ShouldBeFalse("SeaweedFS writes the stored objects");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_resource_name_suffix_does_not_isolate_the_instance(string suffix)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration[ResourceNameSuffixConfigurationKey] = suffix;

        // Act
        var seaweedFs = builder.AddSeaweedFs("seaweed", "media");

        // Assert
        var volume = seaweedFs.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Source.ShouldBe("seaweed-data");
    }
}
