using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Sends the queued page views to Umami one at a time, in order, through the named <c>HttpClient</c> of
/// <see cref="UmamiPageViewServiceCollectionExtensions.HttpClientName"/>, so the host's HTTP client defaults (resilience, timeouts,
/// telemetry) apply. Each page view is its own <c>POST api/send</c> because the visitor's <c>User-Agent</c> and address travel as
/// request headers. A failure is counted and logged without visitor data and never stops the loop. Stopping sends what is queued for
/// at most <see cref="UmamiPageViewOptions.ShutdownTimeout"/>, then cancels and counts the rest.
/// </summary>
/// <param name="queue">The queue of page views.</param>
/// <param name="httpClientFactory">The factory of the named Umami client.</param>
/// <param name="options">The forwarder settings.</param>
/// <param name="metrics">The counter of dropped page views.</param>
/// <param name="timeProvider">The clock of the shutdown timeout.</param>
/// <param name="logger">The logger.</param>
internal sealed class UmamiPageViewSender(
    UmamiPageViewQueue queue,
    IHttpClientFactory httpClientFactory,
    IOptions<UmamiPageViewOptions> options,
    UmamiPageViewMetrics metrics,
    TimeProvider timeProvider,
    ILogger<UmamiPageViewSender> logger) : IHostedService, IDisposable
{
    private const string SendPath = "api/send";

    private readonly CancellationTokenSource _abort = new();
    private Uri? _sendAddress;
    private Task? _sending;
    private int _abandoned;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sendAddress = SendAddress(options.Value.Endpoint!);
        _sending = Task.Run(() => SendQueued(_abort.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Close();
        if (_sending is null)
        {
            return;
        }

        try
        {
            await _sending.WaitAsync(options.Value.ShutdownTimeout, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException || (exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            await _abort.CancelAsync().ConfigureAwait(false);
            await _sending.ConfigureAwait(false);
        }

        var unsent = Volatile.Read(ref _abandoned) + queue.Clear();
        if (unsent > 0)
        {
            metrics.Dropped(WebAnalyticsTelemetry.ShutdownReason, unsent);
            UmamiPageViewLog.UnsentAtShutdown(logger, unsent);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _abort.Dispose();

    /// <summary>Makes the send address below the endpoint, keeping the endpoint's path: <c>https://host/umami</c> sends to <c>https://host/umami/api/send</c>.</summary>
    private static Uri SendAddress(Uri endpoint)
    {
        var path = endpoint.AbsolutePath.EndsWith('/') ? endpoint.AbsoluteUri : endpoint.AbsoluteUri + "/";
        return new Uri(new Uri(path, UriKind.Absolute), SendPath);
    }

    private async Task SendQueued(CancellationToken abort)
    {
        var reader = queue.Reader;
        try
        {
            while (await reader.WaitToReadAsync(abort).ConfigureAwait(false))
            {
                while (!abort.IsCancellationRequested && reader.TryRead(out var pageView))
                {
                    await Send(pageView, abort).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (abort.IsCancellationRequested)
        {
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed page view must never stop the sender; the failure is counted and logged with its type name.")]
    private async Task Send(PageView pageView, CancellationToken abort)
    {
        try
        {
            using var request = Request(pageView);
            using var client = httpClientFactory.CreateClient(UmamiPageViewServiceCollectionExtensions.HttpClientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, abort).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                metrics.Dropped(WebAnalyticsTelemetry.RejectedReason);
                UmamiPageViewLog.SendRejected(logger, (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (abort.IsCancellationRequested)
        {
            Interlocked.Increment(ref _abandoned);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            metrics.Dropped(WebAnalyticsTelemetry.SendFailedReason);
            UmamiPageViewLog.SendFailed(logger, exception.GetType().Name);
        }
    }

    private HttpRequestMessage Request(PageView pageView)
    {
        var body = new UmamiSendRequest
        {
            Payload = new UmamiEventPayload
            {
                Website = pageView.WebsiteId,
                Hostname = pageView.Hostname,
                Url = pageView.Url,
                Referrer = pageView.Referrer,
                Language = pageView.Language,
            },
        };
        var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, UmamiJsonContext.Default.UmamiSendRequest));
        content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
        var request = new HttpRequestMessage(HttpMethod.Post, _sendAddress) { Content = content };
        request.Headers.TryAddWithoutValidation(HeaderNames.UserAgent, pageView.UserAgent);
        if (pageView.ClientIp is { } address)
        {
            request.Headers.TryAddWithoutValidation(options.Value.ClientIpHeader, address.ToString());
        }

        return request;
    }
}
