using System.Threading.RateLimiting;

namespace PandaBear.AspNetCore.Tests;

/// <summary>A rate-limit lease that was not acquired.</summary>
internal sealed class RejectedRateLimitLease : RateLimitLease
{
    /// <inheritdoc />
    public override bool IsAcquired => false;

    /// <inheritdoc />
    public override IEnumerable<string> MetadataNames => [];

    /// <inheritdoc />
    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        metadata = null;
        return false;
    }
}
