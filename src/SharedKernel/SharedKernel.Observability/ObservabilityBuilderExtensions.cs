using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace PandaBear.Observability;

/// <summary>
/// Provides extension methods for configuring reusable OpenTelemetry observability for any host builder.
/// </summary>
public static class ObservabilityBuilderExtensions
{
    /// <summary>
    /// Configures standardized OpenTelemetry logging, metrics, and tracing for the supplied builder,
    /// including cross-service runtime instrumentation plus explicit stable service identity.
    /// Formatted messages and scopes are excluded from OpenTelemetry logs, and <see cref="LogRecordPrivacyProcessor"/>
    /// and <see cref="ActivityPrivacyProcessor"/> are registered, so the privacy baseline applies without a separate call.
    /// Call <see cref="AddTelemetryPrivacy{TBuilder}"/> to add host-owned private attributes.
    /// Call this before adding exporters so the processors run first.
    /// </summary>
    /// <typeparam name="TBuilder">Host builder type, e.g. WebApplicationBuilder, HostApplicationBuilder.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The updated builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRedaction();
        builder.Logging.EnableRedaction();
        builder.EnsureTelemetryPrivacy();

        var serviceVersion = ApplicationVersionProvider.GetEntryAssemblyInformationalVersion();
        var resourceBuilder = ResourceBuilder.CreateDefault().AddDetector(new ExplicitServiceNameDetector(builder.Environment.ApplicationName, serviceVersion));
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resourceBuilder);
                metrics.AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resourceBuilder);
            });

        return builder;
    }

    /// <summary>
    /// Adds host-owned private attribute names to the privacy baseline that <see cref="ConfigureOpenTelemetry{TBuilder}"/>
    /// already applies. When called on its own it applies the baseline too, and the baseline is registered once however
    /// the two methods are combined. Call before adding exporters so the processors run first.
    /// </summary>
    /// <typeparam name="TBuilder">Host builder type, e.g. WebApplicationBuilder, HostApplicationBuilder.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <param name="additionalSensitiveAttribute">Optional host policy naming extra attributes to remove; it cannot permit attributes the shared baseline removes.</param>
    /// <returns>The updated builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder AddTelemetryPrivacy<TBuilder>(this TBuilder builder, Func<string, bool>? additionalSensitiveAttribute = null) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var policy = builder.EnsureTelemetryPrivacy();
        if (additionalSensitiveAttribute is not null)
        {
            policy.Add(additionalSensitiveAttribute);
        }

        return builder;
    }

    private static TelemetryPrivacyPolicy EnsureTelemetryPrivacy<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var existing = builder.Services.FirstOrDefault(static descriptor => descriptor.ServiceType == typeof(TelemetryPrivacyPolicy));
        if (existing?.ImplementationInstance is TelemetryPrivacyPolicy registered)
        {
            return registered;
        }

        var policy = new TelemetryPrivacyPolicy();
        builder.Services.AddSingleton(policy);
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = false;
            logging.IncludeScopes = false;
            logging.AddProcessor(_ => new LogRecordPrivacyProcessor(policy.IsSensitive));
        });
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(new ActivityPrivacyProcessor(policy.IsSensitive)));
        return policy;
    }

    /// <summary>
    /// Adds startup logging for the application name and informational version.
    /// </summary>
    /// <typeparam name="TBuilder">Host builder type, e.g. WebApplicationBuilder, HostApplicationBuilder.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <param name="applicationVersion">The optional application version to log. When omitted, the entry assembly informational version is used.</param>
    /// <returns>The updated builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder AddApplicationVersionLogging<TBuilder>(this TBuilder builder, string? applicationVersion = null) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var applicationName = builder.Environment.ApplicationName;
        var version = applicationVersion ?? ApplicationVersionProvider.GetEntryAssemblyInformationalVersion();
        builder.Services.AddHostedService(provider => new ApplicationVersionLoggingService(
            applicationName,
            version,
            provider.GetRequiredService<ILogger<ApplicationVersionLoggingService>>()));

        return builder;
    }
}
