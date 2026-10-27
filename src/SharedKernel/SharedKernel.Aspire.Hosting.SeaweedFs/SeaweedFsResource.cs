namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a SeaweedFS container with an S3-compatible API.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="accessKeyParameter">The parameter that contains the S3 access key.</param>
/// <param name="secretKeyParameter">The parameter that contains the S3 secret key.</param>
/// <param name="bucketParameter">The parameter that contains the default bucket name.</param>
public sealed class SeaweedFsResource(
    [ResourceName] string name,
    ParameterResource accessKeyParameter,
    ParameterResource secretKeyParameter,
    ParameterResource bucketParameter) : ContainerResource(name)
{
    /// <summary>
    /// The name of the S3 endpoint.
    /// </summary>
    public const string S3EndpointName = "s3";

    /// <summary>
    /// The name of the opt-in local-development Filer UI endpoint added by <c>WithFilerUi</c>.
    /// </summary>
    public const string FilerUiEndpointName = "filer-ui";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the SeaweedFS S3 API.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, S3EndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the parameter that contains the S3 access key.
    /// </summary>
    public ParameterResource AccessKeyParameter { get; internal set; } = accessKeyParameter;

    /// <summary>
    /// Gets the parameter that contains the S3 secret key.
    /// </summary>
    public ParameterResource SecretKeyParameter { get; internal set; } = secretKeyParameter;

    /// <summary>
    /// Gets the parameter that contains the default bucket name.
    /// </summary>
    public ParameterResource BucketParameter { get; } = bucketParameter;

    /// <summary>
    /// Gets the buckets added with <c>AddBucket</c>, keyed by resource name, in addition to the default bucket.
    /// </summary>
    public IReadOnlyDictionary<string, string> Buckets => BucketNames;

    /// <summary>
    /// Gets the mutable map behind <see cref="Buckets"/>.
    /// </summary>
    internal Dictionary<string, string> BucketNames { get; } = new(StringComparer.Ordinal);
}
