namespace PandaBear.Caching.Tests;

/// <summary>A revision store and value loader for <see cref="RevisionCache{TKey, TValue}"/> tests that counts its calls.</summary>
internal sealed class RevisionSource
{
    /// <summary>Gets or sets the revision the next read returns.</summary>
    internal long Revision { get; set; }

    /// <summary>Gets the number of revision reads.</summary>
    internal int RevisionReads { get; private set; }

    /// <summary>Gets the keys and revisions the loader was called with, in order.</summary>
    internal List<(string Key, long Revision)> Loads { get; } = [];

    /// <summary>Gets or sets a load to hand out instead of completing at once; it is used for one call, then cleared.</summary>
    internal TaskCompletionSource<string>? PendingLoad { get; set; }

    /// <summary>Gets or sets a revision read to hand out instead of completing at once; it is used for one call, then cleared.</summary>
    internal TaskCompletionSource<long>? PendingRevision { get; set; }

    /// <summary>Gets or sets the exception the next load throws; it is used for one call, then cleared.</summary>
    internal Exception? LoadFailure { get; set; }

    /// <summary>Reads the revision.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The revision.</returns>
    internal ValueTask<long> ReadRevision(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RevisionReads++;
        if (PendingRevision is { } pending)
        {
            PendingRevision = null;
            return new ValueTask<long>(pending.Task.WaitAsync(ct));
        }

        return ValueTask.FromResult(Revision);
    }

    /// <summary>Loads the value of a key at a revision: <c>key@revision</c>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The value.</returns>
    internal ValueTask<string> Load(string key, long revision, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Loads.Add((key, revision));
        if (LoadFailure is { } failure)
        {
            LoadFailure = null;
            return ValueTask.FromException<string>(failure);
        }

        if (PendingLoad is { } pending)
        {
            PendingLoad = null;
            return new ValueTask<string>(pending.Task);
        }

        return ValueTask.FromResult($"{key}@{revision}");
    }

    /// <summary>Creates a cache over this source.</summary>
    /// <param name="interval">The revision check interval.</param>
    /// <param name="time">The clock.</param>
    /// <returns>The cache.</returns>
    internal RevisionCache<string, string> Cache(TimeSpan interval, TimeProvider time) => new(ReadRevision, Load, interval, time);
}
