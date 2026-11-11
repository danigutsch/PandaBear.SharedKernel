using System.Threading.RateLimiting;

namespace PandaBear.AspNetCore.RateLimiting.Tests;

/// <summary>A rate-limit lease that was not acquired and says when to retry.</summary>
/// <param name="retryAfter">The retry-after metadata.</param>
internal sealed class RetryAfterRateLimitLease(TimeSpan retryAfter) : RateLimitLease
{
    /// <inheritdoc />
    public override bool IsAcquired => false;

    /// <inheritdoc />
    public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

    /// <inheritdoc />
    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
        return metadata is not null;
    }
}
