namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>Time provider that always reports the same instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
