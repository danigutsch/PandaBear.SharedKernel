using System.Threading.Channels;

namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>A page-view target that keeps every page view it receives, so a test can wait for them.</summary>
/// <param name="name">The name of the target.</param>
internal sealed class RecordingPageViewTarget(string name) : IPageViewTarget
{
    private readonly Channel<PageView> _arrived = Channel.CreateUnbounded<PageView>();

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public Task Send(PageView pageView, CancellationToken ct)
    {
        _arrived.Writer.TryWrite(pageView);
        return Task.CompletedTask;
    }

    /// <summary>Waits for the next <paramref name="count"/> page views that arrived.</summary>
    /// <param name="count">How many page views to wait for.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The page views, in the order they arrived.</returns>
    public async Task<IReadOnlyList<PageView>> WaitFor(int count, CancellationToken ct)
    {
        var pageViews = new List<PageView>(count);
        while (pageViews.Count < count)
        {
            pageViews.Add(await _arrived.Reader.ReadAsync(ct));
        }

        return pageViews;
    }
}
