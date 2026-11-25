namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Locks a session within this process; entries are dropped when nobody holds or waits for them.
/// </summary>
internal sealed class InProcessUserSessionLock : IUserSessionLock
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> Acquire(string sessionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        Entry entry;
        lock (_entries)
        {
            if (!_entries.TryGetValue(sessionId, out entry!))
            {
                entry = new Entry();
                _entries.Add(sessionId, entry);
            }

            entry.Users++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            Leave(sessionId, entry);
            throw;
        }

        return new Releaser(this, sessionId, entry);
    }

    private void Leave(string sessionId, Entry entry)
    {
        lock (_entries)
        {
            entry.Users--;
            if (entry.Users == 0)
            {
                _entries.Remove(sessionId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Users { get; set; }
    }

    private sealed class Releaser(InProcessUserSessionLock owner, string sessionId, Entry entry) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                entry.Semaphore.Release();
                owner.Leave(sessionId, entry);
            }

            return ValueTask.CompletedTask;
        }
    }
}
