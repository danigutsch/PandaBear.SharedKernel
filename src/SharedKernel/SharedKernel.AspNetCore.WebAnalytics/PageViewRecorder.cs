using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>
/// The middleware body: runs the rest of the pipeline, then, only for a page view, puts what the targets need on their queues without
/// waiting. It is registered only with a target, so a site without targets captures nothing.
/// </summary>
/// <param name="queues">The queues of the targets.</param>
/// <param name="options">The page-view settings.</param>
internal sealed class PageViewRecorder(PageViewQueues queues, IOptions<PageViewOptions> options)
{
    /// <summary>Answers the request, then records it when it is a page view.</summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when the request is answered and the page view queued.</returns>
    public async Task Invoke(HttpContext context, RequestDelegate next)
    {
        await next(context).ConfigureAwait(false);

        var settings = options.Value;
        if (context.RequestAborted.IsCancellationRequested || !PageViewCapture.IsPageView(context, settings.HonorPrivacySignals))
        {
            return;
        }

        queues.Enqueue(PageViewCapture.Capture(context, settings.Hostname));
    }
}
