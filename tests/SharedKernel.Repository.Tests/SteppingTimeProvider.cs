namespace PandaBear.Repository.Tests;

/// <summary>A clock whose timestamp advances by two seconds at every read, so each timed span lasts exactly two seconds.</summary>
internal sealed class SteppingTimeProvider : TimeProvider
{
    private long _timestamp;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => _timestamp += 2000;
}
