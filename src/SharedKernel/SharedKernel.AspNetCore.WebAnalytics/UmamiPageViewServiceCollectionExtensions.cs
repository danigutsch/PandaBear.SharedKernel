using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>Registers the server-side Umami page-view forwarder.</summary>
public static class UmamiPageViewServiceCollectionExtensions
{
    /// <summary>
    /// The name of the <c>HttpClient</c> that sends page views. It gets the host's HTTP client defaults, such as the resilience and
    /// telemetry of the service defaults; configure it further with <c>services.AddHttpClient(HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "PandaBear.AspNetCore.WebAnalytics.Umami";

    /// <summary>
    /// Registers the forwarder that <c>UseUmamiPageViews</c> feeds: the bounded queue, the background sender, the named
    /// <c>HttpClient</c> and <typeparamref name="TWebsiteIdProvider"/> as the scoped source of the website id. The Umami base address is
    /// read from the configuration value at <paramref name="endpointConfigurationKey"/>; <paramref name="configure"/> runs after it.
    /// The settings are checked when the host starts, so a missing or invalid endpoint stops the host with an
    /// <see cref="OptionsValidationException"/> that names the key.
    /// </summary>
    /// <typeparam name="TWebsiteIdProvider">The application's source of the Umami website id.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="endpointConfigurationKey">
    /// The configuration key of the Umami base address, such as <c>ConnectionStrings:umami</c>, which an Aspire AppHost sets with
    /// <c>WithReference(umami)</c>.
    /// </param>
    /// <param name="configure">Further settings, or <see langword="null"/>.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="endpointConfigurationKey"/> is blank.</exception>
    /// <remarks>
    /// The value is read without the reflection-based configuration binder and the send body is serialized by a source-generated
    /// context, so the registration is trimming- and NativeAOT-safe. The client's logged header values are redacted, so the visitor's
    /// <c>User-Agent</c> and address never reach the HTTP client logs.
    /// </remarks>
    public static IServiceCollection AddUmamiPageViews<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TWebsiteIdProvider>(
        this IServiceCollection services,
        string endpointConfigurationKey,
        Action<UmamiPageViewOptions>? configure = null)
        where TWebsiteIdProvider : class, IUmamiWebsiteIdProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointConfigurationKey);

        services.AddOptions<UmamiPageViewOptions>()
            .Configure<IConfiguration>((options, configuration) => options.Endpoint = Endpoint(configuration[endpointConfigurationKey]))
            .ValidateOnStart();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<UmamiPageViewOptions>>(new UmamiPageViewOptionsValidator(endpointConfigurationKey)));
        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<UmamiPageViewMetrics>();
        services.TryAddSingleton<UmamiPageViewQueue>();
        services.TryAddSingleton<UmamiPageViewRecorder>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, UmamiPageViewSender>());
        services.TryAddScoped<IUmamiWebsiteIdProvider, TWebsiteIdProvider>();
        services.AddHttpClient(HttpClientName).RedactLoggedHeaders(static _ => true);
        return services;
    }

    private static Uri? Endpoint(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ? endpoint : null;
}
