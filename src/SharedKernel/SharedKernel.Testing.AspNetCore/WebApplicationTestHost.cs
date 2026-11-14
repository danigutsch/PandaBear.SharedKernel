using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Testing.AspNetCore;

/// <summary>
/// Creates ASP.NET Core test hosts with consistent bad-request responses and optional environment and service overrides. The hosts do not
/// watch configuration files (<c>hostBuilder:reloadConfigOnChange=false</c>), so parallel test hosts do not exhaust the per-user
/// file-watcher (inotify) limit.
/// </summary>
public static class WebApplicationTestHost
{
    private const string ReloadConfigOnChangeKey = "hostBuilder:reloadConfigOnChange";

    /// <summary>
    /// Creates a web application factory for the target application assembly.
    /// </summary>
    public static WebApplicationFactory<TEntryPoint> Create<TEntryPoint>(
        string? environment = null,
        Action<IServiceCollection>? configureTestServices = null,
        Action<HttpClient>? configureClient = null,
        IReadOnlyDictionary<string, string?>? configuration = null)
        where TEntryPoint : class
    {
        return new ConfigurableWebApplicationFactory<TEntryPoint>(environment, configureTestServices, configureClient, configuration);
    }

    private sealed class ConfigurableWebApplicationFactory<TEntryPoint>(
        string? environment,
        Action<IServiceCollection>? configureTestServices,
        Action<HttpClient>? configureClient,
        IReadOnlyDictionary<string, string?>? configuration)
        : WebApplicationFactory<TEntryPoint>
        where TEntryPoint : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Tests never reload configuration, and one appsettings watcher per file and host exhausts the per-user inotify limit when
            // many test hosts run in parallel. The caller's configuration below can still turn reloading back on.
            builder.UseSetting(ReloadConfigOnChangeKey, "false");

            if (environment is not null)
            {
                HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(
                    builder,
                    environment);
            }

            if (configuration is not null)
            {
                foreach (var (key, value) in configuration)
                {
                    builder.UseSetting(key, value);
                }

                builder.ConfigureAppConfiguration((_, configurationBuilder) => configurationBuilder.AddInMemoryCollection(configuration));
            }

            Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.ConfigureTestServices(builder, services =>
            {
                services.AddTestRequestBindingDefaults();
                configureTestServices?.Invoke(services);
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            configureClient?.Invoke(client);
        }
    }
}
