using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Validates <see cref="S3ObjectStoreOptions" />.
/// </summary>
internal sealed class S3ObjectStoreOptionsValidator : IValidateOptions<S3ObjectStoreOptions>
{
    private static TimeSpan MaximumSupportedTimeout { get; } = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, S3ObjectStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options switch
        {
            { MaxObjectKeyLength: <= 0 } => ValidateOptionsResult.Fail("S3 maximum object key length must be positive."),
            var value when string.IsNullOrWhiteSpace(value.AuthenticationRegion) => ValidateOptionsResult.Fail("S3 authentication region must be provided."),
            { Endpoint: null } => ValidateOptionsResult.Fail("S3 endpoint must be provided."),
            { Endpoint.IsAbsoluteUri: false } => ValidateOptionsResult.Fail("S3 endpoint must be absolute."),
            { Endpoint.Scheme: not ("http" or "https") } => ValidateOptionsResult.Fail("S3 endpoint must use HTTP or HTTPS."),
            var value when string.IsNullOrWhiteSpace(value.Bucket) => ValidateOptionsResult.Fail("S3 bucket must be provided."),
            var value when string.IsNullOrWhiteSpace(value.AccessKey) => ValidateOptionsResult.Fail("S3 access key must be provided."),
            var value when string.IsNullOrWhiteSpace(value.SecretKey) => ValidateOptionsResult.Fail("S3 secret key must be provided."),
            { BucketProvisioningTimeout: var value } when value <= TimeSpan.Zero => ValidateOptionsResult.Fail("S3 bucket provisioning timeout must be positive."),
            { BucketProvisioningTimeout: var value } when value > MaximumSupportedTimeout => ValidateOptionsResult.Fail("S3 bucket provisioning timeout exceeds the supported cancellation timeout."),
            { PublicBaseUri: null } => ValidateOptionsResult.Fail("S3 public base URI must be provided."),
            _ => ValidateOptionsResult.Success
        };
    }
}
