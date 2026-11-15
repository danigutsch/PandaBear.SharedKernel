using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Builds a site with the forwarder registered on <see cref="EndpointKey"/>, so starting it checks the settings.</summary>
internal static class OptionsTestHost
{
    /// <summary>The configuration key of the Umami endpoint, as an Aspire reference sets it.</summary>
    internal const string EndpointKey = "ConnectionStrings:umami";

    /// <summary>Builds, without starting, a site whose endpoint setting is <paramref name="endpoint"/>.</summary>
    /// <param name="endpoint">The configured endpoint, or <see langword="null"/> for none.</param>
    /// <param name="configure">Further settings.</param>
    /// <returns>The site.</returns>
    internal static WebApplication Build(string? endpoint, Action<UmamiPageViewOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration[EndpointKey] = endpoint;
        builder.Services.AddUmamiPageViews<FixedWebsiteIdProvider>(EndpointKey, configure);
        var app = builder.Build();
        app.UseUmamiPageViews();
        return app;
    }
}
