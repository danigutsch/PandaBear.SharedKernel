using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>A dictionary-backed distributed cache that keeps entries until removed and records each entry's options.</summary>
internal sealed class RecordingDistributedCache : IDistributedCache
{
    /// <summary>Stored entries by key.</summary>
    internal ConcurrentDictionary<string, byte[]> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>The options of the last write to each key.</summary>
    internal ConcurrentDictionary<string, DistributedCacheEntryOptions> Options { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        return Entries.TryGetValue(key, out var value) ? value : null;
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return Task.FromResult(Get(key));
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
    }

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        Entries.TryRemove(key, out _);
        Options.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        Entries[key] = value;
        Options[key] = options;
    }

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
