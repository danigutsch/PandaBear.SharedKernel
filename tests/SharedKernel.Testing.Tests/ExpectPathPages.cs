using Microsoft.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>Serves two routed pages, <c>a</c> and <c>b</c>, under an origin that no server answers, for the address-wait tests.</summary>
internal static class ExpectPathPages
{
    /// <summary>Routes the origin on the page: <c>a</c> sends itself to <c>b</c> the given time after its load event, or stays when the delay is <see langword="null"/>; every other path is a plain page.</summary>
    /// <param name="page">The page to route.</param>
    /// <param name="origin">The origin whose requests are fulfilled.</param>
    /// <param name="redirectDelayMilliseconds">The delay after load before <c>a</c> navigates to <c>b</c>, or <see langword="null"/> to stay.</param>
    /// <returns>The routed page.</returns>
    public static async Task<IPage> Route(IPage page, Uri origin, int? redirectDelayMilliseconds)
    {
        var redirect = redirectDelayMilliseconds is { } delay
            ? $"<script>addEventListener('load', () => setTimeout(() => location.href = '/b', {delay}))</script>"
            : string.Empty;
        await page.RouteAsync(origin + "**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            ContentType = "text/html",
            Body = new Uri(route.Request.Url).AbsolutePath == "/a" ? "<p>a</p>" + redirect : "<h1>b</h1>",
        }));
        return page;
    }
}
