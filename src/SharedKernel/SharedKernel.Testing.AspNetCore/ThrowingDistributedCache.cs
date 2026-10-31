using Microsoft.Extensions.Caching.Distributed;
using System.Runtime.ExceptionServices;

namespace PandaBear.Testing.AspNetCore;

/// <summary>An <see cref="IDistributedCache" /> that throws the supplied exception, to prove callers degrade safely. By default every operation throws; with an inner cache and a predicate it throws selectively and forwards the rest. Synchronous members rethrow with the original stack trace preserved; asynchronous members return a task faulted with the exception.</summary>
public sealed class ThrowingDistributedCache : IDistributedCache
{
    private readonly IDistributedCache? _inner;
    private readonly Exception _failure;
    private readonly Func<DistributedCacheOperation, string, int, bool>? _shouldThrow;
    private readonly int[] _calls = new int[Enum.GetValues<DistributedCacheOperation>().Length];

    /// <summary>Initializes a new instance of the <see cref="ThrowingDistributedCache" /> class whose every operation throws.</summary>
    /// <param name="failure">The exception thrown by every operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="failure"/> is <see langword="null"/>.</exception>
    public ThrowingDistributedCache(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        _failure = failure;
    }

    /// <summary>Initializes a new instance of the <see cref="ThrowingDistributedCache" /> class that throws only when <paramref name="shouldThrow" /> returns <see langword="true" /> and forwards other calls to <paramref name="inner" />.</summary>
    /// <param name="inner">The cache that receives every call that does not throw.</param>
    /// <param name="failure">The exception thrown by failing operations.</param>
    /// <param name="shouldThrow">Receives the operation, the key and the 1-based call count of that operation; returns <see langword="true" /> to throw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="failure"/> or <paramref name="shouldThrow"/> is <see langword="null"/>.</exception>
    public ThrowingDistributedCache(IDistributedCache inner, Exception failure, Func<DistributedCacheOperation, string, int, bool> shouldThrow)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(shouldThrow);

        _inner = inner;
        _failure = failure;
        _shouldThrow = shouldThrow;
    }

    /// <summary>Gets the entry options passed to the latest set, whether or not it threw.</summary>
    public DistributedCacheEntryOptions? LastSetOptions
    {
        get; private set;
    }

    /// <summary>Gets the cancellation token passed to the latest remove; <see cref="CancellationToken.None" /> for synchronous removes.</summary>
    public CancellationToken LastRemoveCancellationToken
    {
        get; private set;
    }

    /// <summary>Gets how many sync and async calls of <paramref name="operation" /> were received.</summary>
    /// <param name="operation">The operation to count.</param>
    /// <returns>The number of calls received.</returns>
    public int CallCount(DistributedCacheOperation operation) => Volatile.Read(ref _calls[(int)operation]);

    /// <inheritdoc />
    public byte[]? Get(string key) => Fails(DistributedCacheOperation.Get, key) ? Throw<byte[]?>() : _inner!.Get(key);

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Fails(DistributedCacheOperation.Get, key) ? Task.FromException<byte[]?>(_failure) : _inner!.GetAsync(key, token);

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        LastSetOptions = options;
        if (Fails(DistributedCacheOperation.Set, key))
        {
            Throw();
            return;
        }

        _inner!.Set(key, value, options);
    }

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        LastSetOptions = options;
        return Fails(DistributedCacheOperation.Set, key) ? Task.FromException(_failure) : _inner!.SetAsync(key, value, options, token);
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        if (Fails(DistributedCacheOperation.Refresh, key))
        {
            Throw();
            return;
        }

        _inner!.Refresh(key);
    }

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token = default) =>
        Fails(DistributedCacheOperation.Refresh, key) ? Task.FromException(_failure) : _inner!.RefreshAsync(key, token);

    /// <inheritdoc />
    public void Remove(string key)
    {
        LastRemoveCancellationToken = CancellationToken.None;
        if (Fails(DistributedCacheOperation.Remove, key))
        {
            Throw();
            return;
        }

        _inner!.Remove(key);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        LastRemoveCancellationToken = token;
        return Fails(DistributedCacheOperation.Remove, key) ? Task.FromException(_failure) : _inner!.RemoveAsync(key, token);
    }

    private bool Fails(DistributedCacheOperation operation, string key)
    {
        var call = Interlocked.Increment(ref _calls[(int)operation]);
        return _shouldThrow is null || _shouldThrow(operation, key, call);
    }

    private void Throw() => ExceptionDispatchInfo.Capture(_failure).Throw();

    private T Throw<T>()
    {
        ExceptionDispatchInfo.Capture(_failure).Throw();
        return default;
    }
}
