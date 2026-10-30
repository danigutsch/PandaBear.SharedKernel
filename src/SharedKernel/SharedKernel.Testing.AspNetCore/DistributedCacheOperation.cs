namespace PandaBear.Testing.AspNetCore;

/// <summary>The kind of <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache" /> operation, regardless of sync or async form.</summary>
public enum DistributedCacheOperation
{
    /// <summary>A read of a cached value.</summary>
    Get,

    /// <summary>A write of a cached value.</summary>
    Set,

    /// <summary>A sliding-expiration refresh.</summary>
    Refresh,

    /// <summary>A removal of a cached value.</summary>
    Remove
}
