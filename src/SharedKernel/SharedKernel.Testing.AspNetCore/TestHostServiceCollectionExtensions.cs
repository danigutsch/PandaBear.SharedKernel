using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Testing.AspNetCore;

/// <summary>
/// Service overrides that test hosts of a real application commonly need, for the <c>configureTestServices</c> callback of
/// <see cref="WebApplicationTestHost"/> and <see cref="KestrelTestHost"/>.
/// </summary>
public static class TestHostServiceCollectionExtensions
{
    /// <summary>
    /// Removes every <see cref="IHostedService"/> the application registered (background jobs, queue consumers, warm-up services), so a test
    /// host runs only request handling. The web server itself keeps running: it is not registered in the application's services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection RemoveHostedServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.RemoveAll<IHostedService>();
    }

    /// <summary>
    /// Removes every health check registration, including checks registered after this call, so a test host does not probe databases,
    /// brokers or scanners it never starts. The health endpoints still answer, with no checks to run.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection ClearHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.PostConfigure<HealthCheckServiceOptions>(static options => options.Registrations.Clear());
    }
}
