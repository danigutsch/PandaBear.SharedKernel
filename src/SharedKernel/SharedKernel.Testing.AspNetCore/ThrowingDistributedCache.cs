using Microsoft.Extensions.Caching.Distributed;
using System.Runtime.ExceptionServices;

namespace PandaBear.Testing.AspNetCore;

/// <summary>An <see cref="IDistributedCache" /> whose every operation throws the supplied exception, to prove callers degrade safely. Synchronous members rethrow it with its original stack trace preserved; asynchronous members return a task faulted with it.</summary>
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
    public byte[]? Get(string key) => Throw<byte[]?>();

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(_failure);

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Throw();

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        Task.FromException(_failure);

    /// <inheritdoc />
    public void Refresh(string key) => Throw();

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(_failure);

    /// <inheritdoc />
    public void Remove(string key) => Throw();

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(_failure);

    private void Throw() => ExceptionDispatchInfo.Capture(_failure).Throw();

    private T Throw<T>()
    {
        ExceptionDispatchInfo.Capture(_failure).Throw();
        return default;
    }
}
