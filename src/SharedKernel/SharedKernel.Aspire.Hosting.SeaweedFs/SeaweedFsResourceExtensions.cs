using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SeaweedFS resources to the application model.
/// </summary>
public static class SeaweedFsResourceExtensions
{
    private const string DataPath = "/data";
    private const int FilerPort = 8888;

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
    /// <c>{name}-secret-key</c>, so unsigned requests are rejected. This resource includes built-in health checks of the
    /// SeaweedFS master and the S3 gateway. When this resource is referenced as a dependency using the
    /// <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until the master and the S3 gateway are healthy.
    /// </para>
    /// This version of the package uses the <inheritdoc cref="SeaweedFsContainerImageTags.Image"/> container image, pinned by digest to its <inheritdoc cref="SeaweedFsContainerImageTags.Tag"/> tag.
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
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = resource.AccessKeyParameter;
                context.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = resource.SecretKeyParameter;
                var buckets = new ReferenceExpressionBuilder();
                buckets.AppendFormatted(resource.BucketParameter);
                foreach (var extraBucket in resource.BucketNames.Values)
                {
                    buckets.AppendLiteral("," + extraBucket);
                }

                context.EnvironmentVariables["S3_BUCKET"] = buckets.Build();
            })
            .WithEndpoint(targetPort: 8333, name: SeaweedFsResource.S3EndpointName, scheme: "http", isExternal: false)
            .WithEndpoint(targetPort: 9333, name: "master", scheme: "http", isExternal: false)
            .WithHttpHealthCheck("/cluster/healthz", endpointName: "master")
            .WithHttpHealthCheck("/healthz", endpointName: SeaweedFsResource.S3EndpointName);
    }

    /// <summary>
    /// Replaces the generated S3 access key with a parameter.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="accessKey">The parameter that contains the access key.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SeaweedFsResource> WithAccessKey(this IResourceBuilder<SeaweedFsResource> builder, IResourceBuilder<ParameterResource> accessKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(accessKey);

        RemoveGenerated(builder, builder.Resource.AccessKeyParameter, accessKey.Resource, "access-key");
        builder.Resource.AccessKeyParameter = accessKey.Resource;
        return builder;
    }

    /// <summary>
    /// Replaces the generated S3 secret key with a parameter.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="secretKey">The parameter that contains the secret key.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SeaweedFsResource> WithSecretKey(this IResourceBuilder<SeaweedFsResource> builder, IResourceBuilder<ParameterResource> secretKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(secretKey);

        RemoveGenerated(builder, builder.Resource.SecretKeyParameter, secretKey.Resource, "secret-key");
        builder.Resource.SecretKeyParameter = secretKey.Resource;
        return builder;
    }

    /// <summary>
    /// Adds a bucket that the SeaweedFS container creates at startup, in addition to the default bucket.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the bucket resource.</param>
    /// <param name="bucketName">The name of the bucket. Defaults to <paramref name="name"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> of the bucket resource.</returns>
    /// <remarks>
    /// <c>weed mini</c> creates every bucket named in its comma-separated <c>S3_BUCKET</c> variable and skips buckets that exist.
    /// The bucket resource's connection string carries the S3 endpoint and the bucket name, never the credentials.
    /// </remarks>
    public static IResourceBuilder<SeaweedFsBucketResource> AddBucket(
        this IResourceBuilder<SeaweedFsResource> builder,
        [ResourceName] string name,
        string? bucketName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var bucket = bucketName ?? name;
        if (string.IsNullOrWhiteSpace(bucket) || bucket.Contains(',', StringComparison.Ordinal) || bucket.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("A bucket name must not be blank or contain commas or whitespace.", nameof(bucketName));
        }

        builder.Resource.BucketNames.Add(name, bucket);
        return builder.ApplicationBuilder.AddResource(new SeaweedFsBucketResource(name, bucket, builder.Resource));
    }

    private static void RemoveGenerated(IResourceBuilder<SeaweedFsResource> builder, ParameterResource current, ParameterResource replacement, string suffix)
    {
        if (!ReferenceEquals(current, replacement) && current.Name == $"{builder.Resource.Name}-{suffix}")
        {
            builder.ApplicationBuilder.Resources.Remove(current);
        }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

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

    /// <summary>
    /// Exposes the SeaweedFS Filer UI, which <c>weed mini</c> already serves, on a host endpoint for local development.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// The Filer UI gives unauthenticated read and write access to every stored object. It is a development and operator tool,
    /// not an independently secured service. The endpoint is added only in run mode, so publish manifests never expose it, and
    /// resources that never call this method keep every endpoint internal.
    /// </remarks>
    public static IResourceBuilder<SeaweedFsResource> WithFilerUi(this IResourceBuilder<SeaweedFsResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder;
        }

        return builder
            .WithEndpoint(targetPort: FilerPort, name: SeaweedFsResource.FilerUiEndpointName, scheme: "http", isExternal: true)
            .WithUrlForEndpoint(SeaweedFsResource.FilerUiEndpointName, url => url.DisplayText = "Filer UI");
    }
}
