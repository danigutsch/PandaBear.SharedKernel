namespace PandaBear.Testing;

/// <summary>
/// A time provider that counts how often the current time is read and otherwise delegates to an inner provider. Use it to prove that an
/// operation reads the clock once, so that its steps agree on one instant. Pass a fixed or fake provider as the inner provider to control the time.
/// </summary>
public sealed class CountingTimeProvider : TimeProvider
{
    private readonly TimeProvider inner;
    private int reads;

    /// <summary>Initializes a new instance of the <see cref="CountingTimeProvider"/> class that reads the system clock.</summary>
    public CountingTimeProvider()
        : this(System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CountingTimeProvider"/> class.</summary>
    /// <param name="inner">The provider that supplies the time, the time zone and the timestamps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public CountingTimeProvider(TimeProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        this.inner = inner;
    }

    /// <summary>Gets the number of times the current UTC time was read, including the reads made by <see cref="TimeProvider.GetLocalNow"/>.</summary>
    public int Reads => Volatile.Read(ref reads);

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <inheritdoc />
    public override long TimestampFrequency => inner.TimestampFrequency;

    /// <inheritdoc />
    public override long GetTimestamp() => inner.GetTimestamp();

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        inner.CreateTimer(callback, state, dueTime, period);

    /// <summary>Sets the read count back to zero.</summary>
    public void Reset() => Interlocked.Exchange(ref reads, 0);

    /// <summary>Counts one read and returns the current UTC time of the inner provider.</summary>
    /// <returns>The current UTC time.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        Interlocked.Increment(ref reads);
        return inner.GetUtcNow();
    }
}
