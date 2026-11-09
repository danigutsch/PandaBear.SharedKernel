namespace PandaBear.Packages;

/// <summary>
/// A small in-memory cache whose entries expire after a fixed time measured by a <see cref="TimeProvider"/>. Only
/// answers are stored; callers never store a failure. It holds at most <see cref="MaxEntries"/> entries and stores no
/// more when it is full of unexpired ones.
/// </summary>
/// <typeparam name="T">The cached value.</typeparam>
internal sealed class ExpiringCache<T>
{
    /// <summary>The most entries the cache keeps.</summary>
    public const int MaxEntries = 10_000;

    private readonly Dictionary<string, (T Value, DateTimeOffset Expires)> _entries = [];
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _lifetime;

    /// <summary>Creates a cache.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="lifetime">How long an entry is kept; greater than zero.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is zero or negative.</exception>
    public ExpiringCache(TimeProvider time, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        _time = time;
        _lifetime = lifetime;
    }

    /// <summary>Returns the value for <paramref name="key"/> when it was stored less than the lifetime ago.</summary>
    public bool TryGet(string key, out T value)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (_time.GetUtcNow() < entry.Expires)
                {
                    value = entry.Value;
                    return true;
                }

                _entries.Remove(key);
            }
        }

        value = default!;
        return false;
    }

    /// <summary>Stores <paramref name="value"/> for the lifetime.</summary>
    public void Set(string key, T value)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_entries.Count >= MaxEntries && !_entries.ContainsKey(key))
            {
                foreach (var expired in _entries.Where(e => e.Value.Expires <= now).Select(static e => e.Key).ToArray())
                {
                    _entries.Remove(expired);
                }

                if (_entries.Count >= MaxEntries)
                {
                    return;
                }
            }

            _entries[key] = (value, now + _lifetime);
        }
    }
}
