using System.Collections.Concurrent;

namespace SharedKernel.Caching;

/// <summary>
/// Caches values in memory under the current value of a shared, growing revision number, such as a database row that grows by one in the same
/// transaction as every content change. The revision is read at most once per <c>revisionCheckInterval</c>, so a change saved by any process,
/// including another host, shows up within that interval without a message bus. When a newer revision is seen, every cached value is dropped
/// at once; there is nothing to expire.
/// </summary>
/// <typeparam name="TKey">The key, such as a culture.</typeparam>
/// <typeparam name="TValue">The value; it is shared by every caller, so make it immutable.</typeparam>
/// <remarks>
/// Register one instance per value kind as a singleton. It is thread-safe. Callers that need the revision while it is being read wait for
/// that read instead of starting their own; a caller whose own token is not cancelled reads again when the read it waited for was cancelled
/// by the caller that started it. Two callers that miss the same key at the same time may both load
/// it; the first stored value wins, so the cache never holds two values of one key at one revision. A failed load is not cached. A load that
/// finishes after a newer revision was seen returns its value to its own caller without storing it, and a revision lower than one already
/// seen, such as from a lagging replica, is ignored.
/// </remarks>
public sealed class RevisionCache<TKey, TValue>
    where TKey : notnull
{
    private readonly Func<CancellationToken, ValueTask<long>> _readRevision;
    private readonly Func<TKey, long, CancellationToken, ValueTask<TValue>> _load;
    private readonly TimeSpan _revisionCheckInterval;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private Generation? _current;
    private DateTimeOffset _checkedAt;
    private Task<Generation>? _reading;

    /// <summary>Initializes a new instance of the <see cref="RevisionCache{TKey, TValue}"/> class.</summary>
    /// <param name="readRevision">Reads the current revision.</param>
    /// <param name="load">Loads the value of a key at a revision.</param>
    /// <param name="revisionCheckInterval">The longest time a newer revision can go unnoticed; zero reads it on every call.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <exception cref="ArgumentNullException">A delegate or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionCheckInterval"/> is negative.</exception>
    public RevisionCache(
        Func<CancellationToken, ValueTask<long>> readRevision,
        Func<TKey, long, CancellationToken, ValueTask<TValue>> load,
        TimeSpan revisionCheckInterval,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(readRevision);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentOutOfRangeException.ThrowIfLessThan(revisionCheckInterval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _readRevision = readRevision;
        _load = load;
        _revisionCheckInterval = revisionCheckInterval;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the value of a key at the current revision, loading it when it is not cached.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ct">The cancellation token, passed to the revision read and the load.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public async ValueTask<TValue> Get(TKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);

        var generation = await CurrentGeneration(ct).ConfigureAwait(false);
        if (generation.Values.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await _load(key, generation.Revision, ct).ConfigureAwait(false);
        lock (_gate)
        {
            // A load that finished after a newer revision was seen belongs to a dropped generation: answer it, do not store it.
            return ReferenceEquals(generation, _current) ? generation.Values.GetOrAdd(key, loaded) : loaded;
        }
    }

    private async ValueTask<Generation> CurrentGeneration(CancellationToken ct)
    {
        while (true)
        {
            var now = _timeProvider.GetUtcNow();
            TaskCompletionSource<Generation>? owned = null;
            Task<Generation> reading;
            lock (_gate)
            {
                if (_current is not null && now - _checkedAt < _revisionCheckInterval)
                {
                    return _current;
                }

                if (_reading is null)
                {
                    owned = new TaskCompletionSource<Generation>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _reading = owned.Task;
                }

                reading = _reading;
            }

            if (owned is not null)
            {
                return await ReadGeneration(owned, now, ct).ConfigureAwait(false);
            }

            try
            {
                return await reading.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
            }
        }
    }

    private async ValueTask<Generation> ReadGeneration(TaskCompletionSource<Generation> reading, DateTimeOffset now, CancellationToken ct)
    {
        Generation generation;
        try
        {
            var revision = await _readRevision(ct).ConfigureAwait(false);
            lock (_gate)
            {
                if (_current is null || revision > _current.Revision)
                {
                    _current = new Generation(revision);
                }

                _checkedAt = now > _checkedAt ? now : _checkedAt;
                _reading = null;
                generation = _current;
            }
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _reading = null;
            }

            reading.SetException(exception);
            _ = reading.Task.Exception;
            throw;
        }

        reading.SetResult(generation);
        return generation;
    }

    /// <summary>The values cached at one revision.</summary>
    /// <param name="revision">The revision.</param>
    private sealed class Generation(long revision)
    {
        /// <summary>Gets the revision.</summary>
        internal long Revision { get; } = revision;

        /// <summary>Gets the values by key.</summary>
        internal ConcurrentDictionary<TKey, TValue> Values { get; } = new();
    }
}
