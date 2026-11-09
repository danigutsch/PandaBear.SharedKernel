namespace PandaBear.Testing.Tests;

/// <summary>A time provider frozen at a UTC instant, optionally with a fixed local time zone.</summary>
/// <param name="utcNow">The instant to report.</param>
/// <param name="zone">The local time zone, or <see langword="null"/> for UTC.</param>
internal sealed class FixedClock(DateTimeOffset utcNow, TimeZoneInfo? zone = null) : TimeProvider
{
    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => zone ?? TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;
}
