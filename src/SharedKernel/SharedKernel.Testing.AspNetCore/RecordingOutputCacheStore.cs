using Microsoft.AspNetCore.OutputCaching;

namespace PandaBear.Testing.AspNetCore;

/// <summary>Records output-cache eviction while treating reads as misses and writes as no-ops.</summary>
public sealed class RecordingOutputCacheStore : IOutputCacheStore
{
    /// <summary>Gets whether eviction was attempted.</summary>
    public bool EvictionObserved { get; private set; }
    /// <summary>Gets the last eviction tag.</summary>
    public string? EvictionTag { get; private set; }
    /// <summary>Gets the last eviction cancellation token.</summary>
    public CancellationToken EvictionCancellationToken { get; private set; }
    /// <summary>Gets or sets the exception thrown after recording an eviction.</summary>
    public Exception? EvictionException { get; set; }
    /// <inheritdoc />
    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);
    /// <inheritdoc />
    public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    /// <inheritdoc />
    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        EvictionObserved = true;
        EvictionTag = tag;
        EvictionCancellationToken = cancellationToken;
        if (EvictionException is not null)
        {
            throw EvictionException;
        }
        return ValueTask.CompletedTask;
    }
}
