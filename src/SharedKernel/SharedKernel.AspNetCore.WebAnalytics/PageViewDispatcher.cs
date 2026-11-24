using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Sends the queued page views to their targets: one background loop per target takes one page view at a time, in order, so a slow or
/// failing target holds back only its own queue. A failure is counted for its target and logged without visitor data, and never stops
/// a loop. Stopping sends what is queued for at most <see cref="PageViewOptions.ShutdownTimeout"/>, then cancels and counts the rest.
/// It is registered only with a target, so a site without targets runs no background work.
/// </summary>
/// <param name="queues">The queues of the targets.</param>
/// <param name="options">The page-view settings.</param>
/// <param name="metrics">The counter of dropped page views.</param>
/// <param name="timeProvider">The clock of the shutdown timeout.</param>
/// <param name="logger">The logger.</param>
internal sealed class PageViewDispatcher(
    PageViewQueues queues,
    IOptions<PageViewOptions> options,
    PageViewMetrics metrics,
    TimeProvider timeProvider,
    ILogger<PageViewDispatcher> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _abort = new();
    private Task? _sending;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A target has a blank name, or two targets have the same name.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var abort = _abort.Token;
        _sending = Task.WhenAll(queues.All.Select(queue => Task.Run(() => SendQueued(queue, abort), CancellationToken.None)));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        queues.Close();
        if (_sending is not null)
        {
            try
            {
                await _sending.WaitAsync(options.Value.ShutdownTimeout, timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is TimeoutException || (exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                await _abort.CancelAsync().ConfigureAwait(false);
                await _sending.ConfigureAwait(false);
            }
        }

        foreach (var queue in queues.All)
        {
            var unsent = queue.TakeUnsent();
            if (unsent > 0)
            {
                metrics.Dropped(queue.Name, WebAnalyticsTelemetry.ShutdownReason, unsent);
                PageViewLog.UnsentAtShutdown(logger, unsent, queue.Name);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _abort.Dispose();

    private async Task SendQueued(PageViewTargetQueue queue, CancellationToken abort)
    {
        var reader = queue.Reader;
        try
        {
            while (await reader.WaitToReadAsync(abort).ConfigureAwait(false))
            {
                while (!abort.IsCancellationRequested && reader.TryRead(out var pageView))
                {
                    await Send(queue, pageView, abort).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (abort.IsCancellationRequested)
        {
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failing target must never stop its loop; the failure is counted and logged with its type name.")]
    private async Task Send(PageViewTargetQueue queue, PageView pageView, CancellationToken abort)
    {
        try
        {
            await queue.Target.Send(pageView, abort).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (abort.IsCancellationRequested)
        {
            queue.Abandon();
        }
        catch (HttpRequestException exception) when (exception.StatusCode is { } statusCode)
        {
            metrics.Dropped(queue.Name, WebAnalyticsTelemetry.RejectedReason);
            PageViewLog.SendRejected(logger, queue.Name, (int)statusCode);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            metrics.Dropped(queue.Name, WebAnalyticsTelemetry.SendFailedReason);
            PageViewLog.SendFailed(logger, queue.Name, exception.GetType().FullName ?? exception.GetType().Name);
        }
    }
}
