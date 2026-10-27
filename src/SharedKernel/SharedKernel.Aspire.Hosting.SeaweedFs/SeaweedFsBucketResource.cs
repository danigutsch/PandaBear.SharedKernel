namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a bucket that a SeaweedFS container creates at startup.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="bucketName">The name of the bucket.</param>
/// <param name="parent">The SeaweedFS resource that hosts the bucket.</param>
public sealed class SeaweedFsBucketResource(
    [ResourceName] string name,
    string bucketName,
    SeaweedFsResource parent) : Resource(name), IResourceWithParent<SeaweedFsResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the name of the bucket.
    /// </summary>
    public string BucketName { get; } = bucketName;

    /// <summary>
    /// Gets the SeaweedFS resource that hosts the bucket.
    /// </summary>
    public SeaweedFsResource Parent { get; } = parent;

    /// <summary>
    /// Gets the connection string expression: the S3 endpoint URL and the bucket name, without credentials.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Endpoint={Parent.PrimaryEndpoint.Property(EndpointProperty.Url)};Bucket={BucketName}");
}
