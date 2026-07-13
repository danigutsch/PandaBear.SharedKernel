using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds authenticated local SeaweedFS S3 resources to an Aspire AppHost model.
/// </summary>
public static class SeaweedFsResourceExtensions
{
    private const string ImageTag = "4.39";
    private const string ImageDigest = "c7d6c721b30ae711db766bbbfd40192776e263d4e51e22f57baef7bef93c12c6";
    private const string ResourceNameSuffixConfigurationKey = "DcpPublisher:ResourceNameSuffix";

    /// <summary>
    /// Adds an authenticated SeaweedFS mini cluster with a persistent data volume.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="bucket">The bucket parameter.</param>
    /// <returns>The configured SeaweedFS container resource.</returns>
    public static IResourceBuilder<SeaweedFsResource> AddSeaweedFs(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> bucket)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bucket);

        var accessKey = builder.AddParameter(
            $"{name}-access-key",
            new GenerateParameterDefault { MinLength = 24, Special = false },
            secret: true,
            persist: true);
        var secretKey = builder.AddParameter(
            $"{name}-secret-key",
            new GenerateParameterDefault { MinLength = 32 },
            secret: true,
            persist: true);
        var resourceNameSuffix = builder.Configuration[ResourceNameSuffixConfigurationKey]?.Trim();
        var dataVolumeName = string.IsNullOrWhiteSpace(resourceNameSuffix)
            ? $"{name}-data"
            : $"{name}-{resourceNameSuffix}-data";
        var resource = new SeaweedFsResource(name, accessKey.Resource, secretKey.Resource, bucket.Resource);

        return builder.AddResource(resource)
            .WithImage("chrislusf/seaweedfs", ImageTag)
            .WithImageSHA256(ImageDigest)
            .WithArgs("mini", "-dir=/data", "-webdav=false", "-admin.ui=false")
            .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
            .WithEnvironment("S3_BUCKET", bucket)
            .WithEndpoint(targetPort: 8333, name: SeaweedFsResource.S3EndpointName, scheme: "http", isExternal: false)
            .WithEndpoint(targetPort: 9333, name: "master", scheme: "http", isExternal: false)
            .WithHttpHealthCheck("/cluster/healthz", endpointName: "master")
            .WithVolume(dataVolumeName, "/data");
    }
}
