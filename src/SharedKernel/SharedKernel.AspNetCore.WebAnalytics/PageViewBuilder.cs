using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Adds the targets that page views are sent to; <see cref="PageViewHostApplicationBuilderExtensions.AddPageViews"/> returns it. Like the
/// OTLP exporter of the service defaults, which exists only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, a target extension reads its
/// own setting from <see cref="Configuration"/> and adds its target only when the setting is present. The first target added brings the
/// capture, the queues and the background dispatcher; without one there is none of them.
/// </summary>
public sealed class PageViewBuilder
{
    /// <summary>Initializes the builder.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration.</param>
    internal PageViewBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
    }

    /// <summary>Gets the host's services.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Gets the host's configuration, which a target extension reads its setting from.</summary>
    public IConfiguration Configuration { get; }

    /// <summary>Adds <typeparamref name="TTarget"/>, resolved from the host's services as a singleton, as a target of every page view.</summary>
    /// <typeparam name="TTarget">The target.</typeparam>
    /// <returns>The same builder.</returns>
    /// <remarks>Call it only when the target's setting is present; a target that is added always receives page views.</remarks>
    public PageViewBuilder AddTarget<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>()
        where TTarget : class, IPageViewTarget
    {
        Services.TryAddSingleton<TTarget>();
        return AddTarget(static services => services.GetRequiredService<TTarget>());
    }

    /// <summary>
    /// Adds the target that <paramref name="factory"/> creates as a target of every page view. The factory runs once, when the host
    /// starts, after the settings were checked.
    /// </summary>
    /// <param name="factory">Creates the target from the host's services.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <remarks>Call it only when the target's setting is present; a target that is added always receives page views.</remarks>
    public PageViewBuilder AddTarget(Func<IServiceProvider, IPageViewTarget> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        Services.AddSingleton(new PageViewTargetRegistration(factory));
        Services.AddMetrics();
        Services.TryAddSingleton(TimeProvider.System);
        Services.TryAddSingleton<PageViewMetrics>();
        Services.TryAddSingleton<PageViewQueues>();
        Services.TryAddSingleton<PageViewRecorder>();
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, PageViewDispatcher>());
        return this;
    }
}
