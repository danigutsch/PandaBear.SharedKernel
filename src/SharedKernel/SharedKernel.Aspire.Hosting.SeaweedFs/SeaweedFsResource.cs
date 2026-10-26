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
    public ParameterResource AccessKeyParameter { get; } = accessKeyParameter;

    /// <summary>
    /// Gets the parameter that contains the S3 secret key.
    /// </summary>
    public ParameterResource SecretKeyParameter { get; } = secretKeyParameter;

    /// <summary>
    /// Gets the parameter that contains the default bucket name.
    /// </summary>
    public ParameterResource BucketParameter { get; } = bucketParameter;
}
