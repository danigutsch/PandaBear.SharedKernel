using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Testing.AspNetCore;

/// <summary>
/// Creates ASP.NET Core test hosts with optional environment and service overrides.
/// </summary>
public static class WebApplicationTestHost
{
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

            if (configureTestServices is not null)
            {
                Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.ConfigureTestServices(
                    builder,
                    configureTestServices);
            }
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            configureClient?.Invoke(client);
        }
    }
}
