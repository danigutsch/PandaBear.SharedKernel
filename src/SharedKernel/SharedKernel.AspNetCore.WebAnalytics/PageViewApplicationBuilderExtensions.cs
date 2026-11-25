using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Adds the server-side page-view recording to the request pipeline.</summary>
public static class PageViewApplicationBuilderExtensions
{
    /// <summary>
    /// Records a page view after each request that is one: a GET answered 200 with HTML, not a prefetch or prerender, and, unless turned
    /// off, without <c>Sec-GPC: 1</c> or <c>DNT: 1</c>. It is queued for every target without waiting and sent by a background service, so
    /// no target ever slows or fails a page. Without a target it adds nothing to the pipeline and logs once, at information level, that
    /// page views are off.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><c>AddPageViews</c> was not called.</exception>
    /// <remarks>
    /// Place it after <c>UseForwardedHeaders</c>, so the visitor's address is the one behind your proxy, and before the middleware that
    /// can answer a page without the rest of the pipeline, such as output caching, so cached pages are counted too. Errors re-executed by
    /// the exception handler or status-code pages answer with their error status and are not counted.
    /// </remarks>
    public static IApplicationBuilder UsePageViews(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var services = app.ApplicationServices;
        var registered = services.GetService<IServiceProviderIsService>();
        if (registered?.IsService(typeof(PageViewsRegistered)) != true)
        {
            throw new InvalidOperationException(
                $"Call builder.{nameof(PageViewHostApplicationBuilderExtensions.AddPageViews)}(...) before app.{nameof(UsePageViews)}().");
        }

        if (!registered.IsService(typeof(PageViewRecorder)))
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PageViewApplicationBuilderExtensions));
            PageViewLog.PageViewsOff(logger);
            return app;
        }

        PageViewRecorder? recorder = null;
        return app.Use((context, next) => (recorder ??= services.GetRequiredService<PageViewRecorder>()).Invoke(context, next));
    }
}
