namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Time provider whose UTC clock stays fixed until a test moves it forward.
/// </summary>
/// <param name="utcNow">The initial UTC time reported by the provider.</param>
internal sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => utcNow;

    /// <summary>
    /// Moves the reported UTC time forward by the given duration.
    /// </summary>
    public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
}
