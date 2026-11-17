using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PandaBear.Testing.Logging;
using PandaBear.Testing.Metrics;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Builds the page-view services with the Umami target, without a web server, to drive the queues and the dispatcher directly.</summary>
internal static class SenderTestServices
{
    /// <summary>Builds, without starting, a host with a fake Umami, a meter collector, and optionally a log recorder, a clock and further targets.</summary>
    /// <param name="umami">The fake Umami.</param>
    /// <param name="meters">The collector of the page-view measurements.</param>
    /// <param name="configure">Further settings.</param>
    /// <param name="logs">The log recorder, or <see langword="null"/>.</param>
    /// <param name="time">The clock, or <see langword="null"/> for the system clock.</param>
    /// <param name="addTargets">Adds further targets.</param>
    /// <returns>The host.</returns>
    internal static IHost Build(
        HttpMessageHandler umami,
        MeterCollector meters,
        Action<PageViewOptions>? configure = null,
        RecordingLoggerProvider? logs = null,
        TimeProvider? time = null,
        Action<PageViewBuilder>? addTargets = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
        builder.Configuration[UmamiConnections.Key] = UmamiConnections.Default;
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }

        builder.Services.AddSingleton<IMeterFactory>(meters);
        if (time is not null)
        {
            builder.Services.AddSingleton(time);
        }

        var pageViews = builder.AddPageViews(configure).AddUmami();
        addTargets?.Invoke(pageViews);
        builder.Services.AddHttpClient(UmamiPageViewBuilderExtensions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => umami);
        return builder.Build();
    }

    /// <summary>Gets the background dispatcher among the hosted services.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The dispatcher.</returns>
    internal static PageViewDispatcher Dispatcher(IHost host) =>
        host.Services.GetServices<IHostedService>().OfType<PageViewDispatcher>().ShouldHaveSingleItem();

    /// <summary>Gets the queues of the targets.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The queues.</returns>
    internal static PageViewQueues Queues(IHost host) => host.Services.GetRequiredService<PageViewQueues>();

    /// <summary>Creates a page view of <paramref name="url"/> by the test visitor.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The page view.</returns>
    internal static PageView PageView(string url) => new()
    {
        PathAndQuery = url,
        UserAgent = PageContexts.VisitorAgent,
        ClientIp = PageContexts.VisitorAddress,
    };
}
