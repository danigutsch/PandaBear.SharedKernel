using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Seo;

/// <summary>Registers the canonical origin of a public site.</summary>
public static class CanonicalOriginServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="CanonicalOriginOptions.Origin"/> from the configuration value at <paramref name="configurationKey"/> and validates it
    /// when the host starts: it must be an absolute http or https origin without a path, query, fragment or user info
    /// (<see cref="SitemapCanonicalOrigin.IsValid"/>). A host with a missing or invalid origin fails to start with an
    /// <see cref="OptionsValidationException"/> that names the setting, instead of serving pages with wrong canonical links.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configurationKey">The configuration key, such as <c>PublicSite:Origin</c> (<c>PublicSite__Origin</c> as an environment variable).</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="configurationKey"/> is blank.</exception>
    /// <remarks>The value is read without the reflection-based configuration binder, so the registration is trimming- and NativeAOT-safe.</remarks>
    public static IServiceCollection AddCanonicalOrigin(this IServiceCollection services, string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);

        services.AddOptions<CanonicalOriginOptions>()
            .Configure<IConfiguration>((options, configuration) => options.Origin = configuration[configurationKey])
            .Validate(
                static options => SitemapCanonicalOrigin.IsValid(options.Origin),
                $"Set {configurationKey} to the site's absolute http or https origin without a path, such as https://example.com; canonical links and the sitemap are built from it.")
            .ValidateOnStart();
        return services;
    }
}
