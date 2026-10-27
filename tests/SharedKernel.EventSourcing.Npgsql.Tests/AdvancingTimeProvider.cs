namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>A fake clock that returns its configured instant plus one extra second for each prior read, and counts how often it was read.</summary>
/// <param name="instant">The instant returned by the first read.</param>
internal sealed class AdvancingTimeProvider(DateTimeOffset instant) : TimeProvider
{
    /// <summary>Gets or sets the base instant that the next read is offset from.</summary>
    public DateTimeOffset Instant { get; set; } = instant;

    /// <summary>Gets how many times the clock has been read.</summary>
    public int Reads
    {
        get; private set;
    }

    public override DateTimeOffset GetUtcNow()
    {
        Reads++;
        return Instant.AddSeconds(Reads - 1);
    }
}
