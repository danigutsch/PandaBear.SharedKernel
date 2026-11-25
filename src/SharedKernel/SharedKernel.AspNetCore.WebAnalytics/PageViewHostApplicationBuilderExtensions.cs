using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Registers server-side, cookieless page views and returns the builder that adds their targets.</summary>
public static class PageViewHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the page-view settings that every target shares and returns the builder that adds the targets, such as
    /// <see cref="UmamiPageViewBuilderExtensions.AddUmami"/>. Page views are recorded only when at least one target is added; without one,
    /// <c>UsePageViews</c> adds nothing to the pipeline and no queue or background service exists.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="configure">Further settings, or <see langword="null"/>.</param>
    /// <returns>The builder of the targets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The settings are checked when the host starts, so an invalid one stops the host with an <see cref="OptionsValidationException"/>
    /// that names the setting. Nothing is read with the reflection-based configuration binder, so the registration is trimming- and
    /// NativeAOT-safe.
    /// </remarks>
    public static PageViewBuilder AddPageViews(this IHostApplicationBuilder builder, Action<PageViewOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        services.AddOptions<PageViewOptions>().ValidateOnStart();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PageViewOptions>, PageViewOptionsValidator>());
        services.TryAddSingleton<PageViewsRegistered>();
        return new PageViewBuilder(services, builder.Configuration);
    }
}
