namespace PandaBear.BuildingBlocks;

/// <summary>
/// A <see cref="TimeProvider"/> that reports a configured business time zone as its local time zone, so
/// <see cref="TimeProvider.GetLocalNow"/> and <see cref="TimeProviderExtensions.Today"/> follow the business day instead of the
/// server's zone. UTC time comes from the inner provider.
/// </summary>
public sealed class BusinessTimeProvider : TimeProvider
{
    private readonly TimeProvider _inner;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="businessTimeZone">The business time zone reported as the local zone.</param>
    /// <param name="inner">The provider for UTC time and timers; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="businessTimeZone"/> is <see langword="null"/>.</exception>
    public BusinessTimeProvider(TimeZoneInfo businessTimeZone, TimeProvider? inner = null)
    {
        ArgumentNullException.ThrowIfNull(businessTimeZone);

        LocalTimeZone = businessTimeZone;
        _inner = inner ?? System;
    }

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone
    {
        get;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

    /// <inheritdoc />
    public override long GetTimestamp() => _inner.GetTimestamp();

    /// <inheritdoc />
    public override long TimestampFrequency => _inner.TimestampFrequency;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _inner.CreateTimer(callback, state, dueTime, period);
}
