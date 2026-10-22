namespace PandaBear.EventSourcing.Npgsql.Tests;

internal sealed class AdvancingTimeProvider(DateTimeOffset instant) : TimeProvider
{
    public DateTimeOffset Instant { get; set; } = instant;

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
