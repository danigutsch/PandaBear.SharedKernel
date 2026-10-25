using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds authenticated local SeaweedFS S3 resources to an Aspire AppHost model.
/// </summary>
public static class SeaweedFsResourceExtensions
{
    private const string ImageTag = "4.39";
    private const string ImageDigest = "c7d6c721b30ae711db766bbbfd40192776e263d4e51e22f57baef7bef93c12c6";
    private const string DataPath = "/data";
    private const string ResourceNameSuffixConfigurationKey = "DcpPublisher:ResourceNameSuffix";

    /// <summary>
    /// Adds an authenticated SeaweedFS mini cluster whose data persists between AppHost runs.
    /// </summary>
    /// <remarks>
    /// The data lives in the named volume <c>{name}-data</c>, so a restarted AppHost reuses it.
    /// When the <c>DcpPublisher:ResourceNameSuffix</c> configuration value is set, the resource is an isolated instance,
    /// such as a parallel test run. It then gets an anonymous volume that the container runtime removes together with
    /// the container, because a named volume of one isolated instance is never reused and would stay behind.
    /// </remarks>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="bucketDefault">The default bucket name.</param>
    /// <returns>The configured SeaweedFS container resource.</returns>
    public static IResourceBuilder<SeaweedFsResource> AddSeaweedFs(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string bucketDefault)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketDefault);

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
        var bucket = builder.AddParameter($"{name}-bucket", bucketDefault);
        var isolatedInstance = !string.IsNullOrWhiteSpace(builder.Configuration[ResourceNameSuffixConfigurationKey]);
        var resource = new SeaweedFsResource(name, accessKey.Resource, secretKey.Resource, bucket.Resource);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage("chrislusf/seaweedfs", ImageTag)
            .WithImageRegistry("docker.io")
            .WithImageSHA256(ImageDigest)
            .WithArgs("mini", "-dir=/data", "-webdav=false", "-admin.ui=false")
            .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
            .WithEnvironment("S3_BUCKET", bucket)
            .WithEndpoint(targetPort: 8333, name: SeaweedFsResource.S3EndpointName, scheme: "http", isExternal: false)
            .WithEndpoint(targetPort: 9333, name: "master", scheme: "http", isExternal: false)
            .WithHttpHealthCheck("/cluster/healthz", endpointName: "master");

        return isolatedInstance
            ? resourceBuilder.WithVolume(DataPath)
            : resourceBuilder.WithVolume($"{name}-data", DataPath);
    }
}
