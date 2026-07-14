using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Configures host services that must remain local and ephemeral during build-time OpenAPI generation.
/// </summary>
public static class OpenApiBuildGenerationServiceCollectionExtensions
{
    /// <summary>
    /// Configures an ephemeral data-protection provider only for the OpenAPI document generator process.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddOpenApiBuildGenerationDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (OpenApiBuildGeneration.IsEnabled(configuration))
        {
            AddEphemeralDataProtection(services);
        }

        return services;
    }

    internal static void AddEphemeralDataProtection(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDataProtection().UseEphemeralDataProtectionProvider();
    }
}
