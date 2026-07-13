using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents an authenticated SeaweedFS S3-compatible container and its configured parameters.
/// </summary>
public sealed class SeaweedFsResource(
    string name,
    ParameterResource accessKeyParameter,
    ParameterResource secretKeyParameter,
    ParameterResource bucketParameter) : ContainerResource(name)
{
    /// <summary>
    /// Gets the SeaweedFS S3 endpoint name.
    /// </summary>
    public const string S3EndpointName = "s3";

    /// <summary>
    /// Gets the owned S3 access-key parameter.
    /// </summary>
    public ParameterResource AccessKeyParameter { get; } = accessKeyParameter;

    /// <summary>
    /// Gets the owned S3 secret-key parameter.
    /// </summary>
    public ParameterResource SecretKeyParameter { get; } = secretKeyParameter;

    /// <summary>
    /// Gets the caller-provided S3 bucket parameter.
    /// </summary>
    public ParameterResource BucketParameter { get; } = bucketParameter;
}
