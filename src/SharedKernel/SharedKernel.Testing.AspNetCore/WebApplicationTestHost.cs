using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        Action<IServiceCollection>? configureTestServices = null)
        where TEntryPoint : class
    {
        return new ConfigurableWebApplicationFactory<TEntryPoint>(environment, configureTestServices);
    }

    private sealed class ConfigurableWebApplicationFactory<TEntryPoint>(
        string? environment,
        Action<IServiceCollection>? configureTestServices)
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

            if (configureTestServices is not null)
            {
                Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.ConfigureTestServices(
                    builder,
                    configureTestServices);
            }
        }
    }
}
