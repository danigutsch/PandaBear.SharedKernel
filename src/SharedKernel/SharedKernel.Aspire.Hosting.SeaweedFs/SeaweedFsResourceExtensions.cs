using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SeaweedFS resources to the application model.
/// </summary>
public static class SeaweedFsResourceExtensions
{
    private const string DataPath = "/data";

    /// <summary>
    /// Adds a SeaweedFS container with an authenticated S3-compatible API to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="bucketDefault">The default bucket name.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// The S3 access key and secret key are generated as the secret parameters <c>{name}-access-key</c> and
    /// <c>{name}-secret-key</c>, so unsigned requests are rejected. This resource includes built-in health checks.
    /// When this resource is referenced as a dependency using the
    /// <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until SeaweedFS is able to service requests.
    /// </para>
    /// This version of the package defaults to the <inheritdoc cref="SeaweedFsContainerImageTags.Tag"/> tag of the <inheritdoc cref="SeaweedFsContainerImageTags.Image"/> container image.
    /// </remarks>
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
        var resource = new SeaweedFsResource(name, accessKey.Resource, secretKey.Resource, bucket.Resource);

        return builder.AddResource(resource)
            .WithImage(SeaweedFsContainerImageTags.Image, SeaweedFsContainerImageTags.Tag)
            .WithImageRegistry(SeaweedFsContainerImageTags.Registry)
            .WithImageSHA256(SeaweedFsContainerImageTags.Digest)
            .WithArgs("mini", "-dir=/data", "-webdav=false", "-admin.ui=false")
            .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
            .WithEnvironment("S3_BUCKET", bucket)
            .WithEndpoint(targetPort: 8333, name: SeaweedFsResource.S3EndpointName, scheme: "http", isExternal: false)
            .WithEndpoint(targetPort: 9333, name: "master", scheme: "http", isExternal: false)
            .WithHttpHealthCheck("/cluster/healthz", endpointName: "master");
    }

    /// <summary>
    /// Adds a named volume for the data folder to a SeaweedFS container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SeaweedFsResource> WithDataVolume(this IResourceBuilder<SeaweedFsResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DataPath, isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a SeaweedFS container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SeaweedFsResource> WithDataBindMount(this IResourceBuilder<SeaweedFsResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, DataPath, isReadOnly);
    }

    /// <summary>
    /// Configures the host port that the SeaweedFS S3 endpoint is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SeaweedFsResource> WithHostPort(this IResourceBuilder<SeaweedFsResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(SeaweedFsResource.S3EndpointName, endpoint => endpoint.Port = port);
    }
}
