namespace PandaBear.Versioning.Tests;

/// <summary>
/// Time provider that always returns the same UTC time.
/// </summary>
/// <param name="utcNow">The time returned by every call to GetUtcNow.</param>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
