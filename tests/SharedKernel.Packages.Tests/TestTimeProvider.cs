namespace PandaBear.Packages.Tests;

/// <summary>A clock that only moves when a test moves it.</summary>
internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan by) => _now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
