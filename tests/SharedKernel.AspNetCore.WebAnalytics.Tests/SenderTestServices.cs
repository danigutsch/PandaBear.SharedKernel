using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PandaBear.Testing.Logging;
using PandaBear.Testing.Metrics;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Builds the forwarder's services without a web host, to drive the queue and the sender directly.</summary>
internal static class SenderTestServices
{
    /// <summary>Builds the services with a fake Umami, a meter collector, and optionally a log recorder and a clock.</summary>
    /// <param name="umami">The fake Umami.</param>
    /// <param name="meters">The collector of the forwarder's measurements.</param>
    /// <param name="configure">Further settings.</param>
    /// <param name="logs">The log recorder, or <see langword="null"/>.</param>
    /// <param name="time">The clock, or <see langword="null"/> for the system clock.</param>
    /// <returns>The services.</returns>
    internal static ServiceProvider Build(
        HttpMessageHandler umami,
        MeterCollector meters,
        Action<UmamiPageViewOptions>? configure = null,
        RecordingLoggerProvider? logs = null,
        TimeProvider? time = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(PageViewTestApp.EndpointKey, "http://umami.test/")])
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging =>
        {
            if (logs is not null)
            {
                logging.AddProvider(logs);
            }
        });
        services.AddSingleton<IMeterFactory>(meters);
        if (time is not null)
        {
            services.AddSingleton(time);
        }

        services.AddUmamiPageViews<FixedWebsiteIdProvider>(PageViewTestApp.EndpointKey, configure);
        services.AddHttpClient(UmamiPageViewServiceCollectionExtensions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => umami);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Gets the background sender among the hosted services.</summary>
    /// <param name="services">The services.</param>
    /// <returns>The sender.</returns>
    internal static UmamiPageViewSender Sender(IServiceProvider services) =>
        services.GetServices<IHostedService>().OfType<UmamiPageViewSender>().ShouldHaveSingleItem();

    /// <summary>Creates a page view of <paramref name="url"/> by the test visitor.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The page view.</returns>
    internal static PageView PageView(string url) => new()
    {
        WebsiteId = FixedWebsiteIdProvider.WebsiteId,
        Url = url,
        UserAgent = PageContexts.VisitorAgent,
        ClientIp = PageContexts.VisitorAddress,
    };
}
