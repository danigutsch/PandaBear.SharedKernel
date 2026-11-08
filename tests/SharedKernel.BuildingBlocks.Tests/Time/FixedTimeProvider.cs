namespace PandaBear.BuildingBlocks.Tests;

/// <summary>A time provider that reports one fixed instant, in UTC.</summary>
/// <param name="utcNow">The instant to report.</param>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;
}
