using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// The bounded queue between the requests that record page views and the sender. Adding never waits: a full queue, or one closed
/// because the host is stopping, drops the page view and counts it.
/// </summary>
internal sealed class UmamiPageViewQueue
{
    private readonly Lazy<Channel<PageView>> _channel;
    private readonly UmamiPageViewMetrics _metrics;
    private volatile bool _closed;

    /// <summary>Initializes the queue; its capacity is read on first use, after the settings were checked at start.</summary>
    /// <param name="options">The forwarder settings.</param>
    /// <param name="metrics">The counter of dropped page views.</param>
    public UmamiPageViewQueue(IOptions<UmamiPageViewOptions> options, UmamiPageViewMetrics metrics)
    {
        _metrics = metrics;
        _channel = new(() => Channel.CreateBounded<PageView>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        }));
    }

    /// <summary>Gets the reader the sender takes page views from.</summary>
    public ChannelReader<PageView> Reader => _channel.Value.Reader;

    /// <summary>Adds a page view without waiting.</summary>
    /// <param name="pageView">The page view.</param>
    /// <returns><see langword="true"/> when it was queued; <see langword="false"/> when it was dropped and counted.</returns>
    public bool TryEnqueue(PageView pageView)
    {
        if (_channel.Value.Writer.TryWrite(pageView))
        {
            return true;
        }

        _metrics.Dropped(_closed ? WebAnalyticsTelemetry.ShutdownReason : WebAnalyticsTelemetry.QueueFullReason);
        return false;
    }

    /// <summary>Closes the queue: what is queued can still be read, and anything added from now on is dropped.</summary>
    public void Close()
    {
        _closed = true;
        _channel.Value.Writer.TryComplete();
    }

    /// <summary>Removes what is still queued without sending it.</summary>
    /// <returns>How many page views were removed.</returns>
    public int Clear()
    {
        var count = 0;
        while (_channel.Value.Reader.TryRead(out _))
        {
            count++;
        }

        return count;
    }
}
