namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>A page-view target whose every send throws, with a message that must never reach a log.</summary>
/// <param name="name">The name of the target.</param>
internal sealed class FailingPageViewTarget(string name) : IPageViewTarget
{
    /// <summary>The message of the failure.</summary>
    internal const string FailureMessage = "target-secret";

    private int _calls;

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <summary>Gets how many page views the target was asked to send.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc />
    public Task Send(PageView pageView, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        throw new InvalidOperationException(FailureMessage);
    }
}
