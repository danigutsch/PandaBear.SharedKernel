using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests what registering the S3 object store adds to a host's telemetry.</summary>
public sealed class S3ObjectStoreRegistrationTests
{
    [Fact]
    public void Registering_the_store_builds_a_meter_provider_with_the_aws_sdk_metrics()
    {
        // Arrange
        var expected = S3ObjectStoreOptionsTestFactory.CreateValidOptions();
        var services = new ServiceCollection();
        services.AddS3ObjectStore(options =>
        {
            options.Endpoint = expected.Endpoint;
            options.PublicBaseUri = expected.PublicBaseUri;
            options.Bucket = expected.Bucket;
            options.AccessKey = expected.AccessKey;
            options.SecretKey = expected.SecretKey;
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var meterProvider = provider.GetService<MeterProvider>();

        // Assert
        meterProvider.ShouldNotBeNull();
    }
}
