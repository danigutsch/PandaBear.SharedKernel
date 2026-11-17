using System.Threading.Channels;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// The bounded queue between the requests that record page views and one target. Adding never waits: a full queue, or one closed
/// because the host is stopping, drops the page view and counts it for this target.
/// </summary>
internal sealed class PageViewTargetQueue
{
    private readonly Channel<PageView> _channel;
    private readonly PageViewMetrics _metrics;
    private volatile bool _closed;
    private int _abandoned;

    /// <summary>Initializes the queue of <paramref name="target"/>.</summary>
    /// <param name="target">The target that the queued page views are sent to.</param>
    /// <param name="capacity">How many page views wait at most.</param>
    /// <param name="metrics">The counter of dropped page views.</param>
    public PageViewTargetQueue(IPageViewTarget target, int capacity, PageViewMetrics metrics)
    {
        Target = target;
        Name = target.Name;
        _metrics = metrics;
        _channel = Channel.CreateBounded<PageView>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Gets the target.</summary>
    public IPageViewTarget Target { get; }

    /// <summary>Gets the name of the target, read once.</summary>
    public string Name { get; }

    /// <summary>Gets the reader the dispatcher takes page views from.</summary>
    public ChannelReader<PageView> Reader => _channel.Reader;

    /// <summary>Adds a page view without waiting, or drops and counts it.</summary>
    /// <param name="pageView">The page view.</param>
    public void Enqueue(PageView pageView)
    {
        if (!_channel.Writer.TryWrite(pageView))
        {
            _metrics.Dropped(Name, _closed ? WebAnalyticsTelemetry.ShutdownReason : WebAnalyticsTelemetry.QueueFullReason);
        }
    }

    /// <summary>Closes the queue: what is queued can still be read, and anything added from now on is dropped.</summary>
    public void Close()
    {
        _closed = true;
        _channel.Writer.TryComplete();
    }

    /// <summary>Notes a page view that was in flight when sending was given up.</summary>
    public void Abandon() => Interlocked.Increment(ref _abandoned);

    /// <summary>Removes what is still queued without sending it and adds the abandoned page views.</summary>
    /// <returns>How many page views were not sent.</returns>
    public int TakeUnsent()
    {
        var count = Interlocked.Exchange(ref _abandoned, 0);
        while (_channel.Reader.TryRead(out _))
        {
            count++;
        }

        return count;
    }
}
