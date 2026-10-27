namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Configures the authenticated S3-compatible object store.
/// </summary>
public sealed class S3ObjectStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum accepted object key length.
    /// </summary>
    public int MaxObjectKeyLength { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the S3 signing region.
    /// </summary>
    public string AuthenticationRegion { get; set; } = "us-east-1";

    /// <summary>
    /// Gets or sets whether bucket names are placed in the endpoint path.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Gets or sets the private S3 endpoint.
    /// </summary>
    public Uri? Endpoint
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the private bucket name.
    /// </summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the S3 access key.
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the S3 secret key.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the adapter may create the bucket when it is absent.
    /// </summary>
    /// <remarks>
    /// Enable only for explicit local development. Production deployments must pre-provision the bucket.
    /// </remarks>
    public bool AutoProvisionBucket
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the maximum duration for local bucket auto-provisioning.
    /// </summary>
    public TimeSpan BucketProvisioningTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the public base URI for published objects.
    /// </summary>
    public Uri PublicBaseUri { get; set; } = new("/objects/", UriKind.Relative);
}
