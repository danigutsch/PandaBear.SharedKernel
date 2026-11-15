using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// The middleware body: runs the rest of the pipeline, then, only for a page view with a website id, queues what Umami needs without
/// waiting. Nothing it does after the page can fail the page.
/// </summary>
/// <param name="queue">The queue of page views.</param>
/// <param name="options">The forwarder settings.</param>
/// <param name="logger">The logger.</param>
internal sealed class UmamiPageViewRecorder(UmamiPageViewQueue queue, IOptions<UmamiPageViewOptions> options, ILogger<UmamiPageViewRecorder> logger)
{
    /// <summary>Answers the request, then records it when it is a page view.</summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when the request and the recording are done.</returns>
    public async Task Invoke(HttpContext context, RequestDelegate next)
    {
        await next(context).ConfigureAwait(false);

        var settings = options.Value;
        if (context.RequestAborted.IsCancellationRequested || !PageViewCapture.IsPageView(context, settings.HonorPrivacySignals))
        {
            return;
        }

        if (await WebsiteId(context).ConfigureAwait(false) is { } websiteId && websiteId != Guid.Empty)
        {
            queue.TryEnqueue(PageViewCapture.Capture(context, websiteId, settings.Hostname));
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The page is already answered; a failing lookup is logged with its type name and the page view is not recorded.")]
    private async ValueTask<Guid?> WebsiteId(HttpContext context)
    {
        try
        {
            var provider = context.RequestServices.GetRequiredService<IUmamiWebsiteIdProvider>();
            return await provider.GetWebsiteId(context, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            UmamiPageViewLog.WebsiteIdLookupFailed(logger, exception.GetType().Name);
            return null;
        }
    }
}
