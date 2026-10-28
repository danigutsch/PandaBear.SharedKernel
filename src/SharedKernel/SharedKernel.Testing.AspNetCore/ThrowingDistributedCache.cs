using Microsoft.Extensions.Caching.Distributed;

namespace PandaBear.Testing.AspNetCore;

/// <summary>An <see cref="IDistributedCache" /> whose every operation throws the supplied exception, to prove callers degrade safely.</summary>
public sealed class ThrowingDistributedCache : IDistributedCache
{
    private readonly Exception _failure;

    /// <summary>Initializes a new instance of the <see cref="ThrowingDistributedCache" /> class.</summary>
    /// <param name="failure">The exception thrown by every operation.</param>
    public ThrowingDistributedCache(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        _failure = failure;
    }

    /// <inheritdoc />
    public byte[]? Get(string key) => throw _failure;

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(_failure);

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw _failure;

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        Task.FromException(_failure);

    /// <inheritdoc />
    public void Refresh(string key) => throw _failure;

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(_failure);

    /// <inheritdoc />
    public void Remove(string key) => throw _failure;

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(_failure);
}
