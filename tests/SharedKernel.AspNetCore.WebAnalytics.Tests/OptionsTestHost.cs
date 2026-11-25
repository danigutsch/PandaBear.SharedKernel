using Microsoft.AspNetCore.Builder;

namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>Builds a site with page views and the Umami target on <see cref="UmamiConnections.Key"/>, so starting it checks the settings.</summary>
internal static class OptionsTestHost
{
    /// <summary>Builds, without starting, a site whose Umami connection string is <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The Umami connection string, or <see langword="null"/> for none.</param>
    /// <param name="configure">Further page-view settings.</param>
    /// <param name="configureUmami">Further Umami target settings.</param>
    /// <param name="addTargets">Adds further targets.</param>
    /// <returns>The site.</returns>
    internal static WebApplication Build(
        string? connectionString,
        Action<PageViewOptions>? configure = null,
        Action<UmamiPageViewTargetOptions>? configureUmami = null,
        Action<PageViewBuilder>? addTargets = null)
    {
        var builder = TestWebHosts.CreateBuilder();
        builder.Configuration[UmamiConnections.Key] = connectionString;
        var pageViews = builder.AddPageViews(configure).AddUmami(configure: configureUmami);
        addTargets?.Invoke(pageViews);
        var app = builder.Build();
        app.UsePageViews();
        return app;
    }
}
