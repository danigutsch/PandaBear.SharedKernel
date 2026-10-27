using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Health check that lists at most one key of the configured bucket, so it reports healthy only when the endpoint, credentials, and bucket work.
/// </summary>
/// <param name="client">The S3 client.</param>
/// <param name="options">The validated store options.</param>
internal sealed class S3ObjectStoreHealthCheck(IAmazonS3 client, IOptions<S3ObjectStoreOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var operation = S3ObjectStoreTelemetry.Start("health");
        try
        {
            _ = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = options.Value.Bucket, MaxKeys = 1 }, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (AmazonServiceException)
        {
            return HealthCheckResult.Unhealthy("Object storage is unavailable.");
        }
        catch (AmazonClientException)
        {
            return HealthCheckResult.Unhealthy("Object storage is unavailable.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Unhealthy("Object storage is unavailable.");
        }
    }
}
