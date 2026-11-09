namespace PandaBear.Packages.Tool.Tests;

/// <summary>A clock that always reports the same time.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
