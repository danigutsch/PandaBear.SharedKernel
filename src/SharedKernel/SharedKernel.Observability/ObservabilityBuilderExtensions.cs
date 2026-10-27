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
    /// </summary>
    /// <typeparam name="TBuilder">Host builder type, e.g. WebApplicationBuilder, HostApplicationBuilder.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The updated builder instance for chaining.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRedaction();
        builder.Logging.EnableRedaction();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

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
    /// Excludes formatted messages and scopes from OpenTelemetry logs and registers
    /// <see cref="LogRecordPrivacyProcessor"/> and <see cref="ActivityPrivacyProcessor"/>.
    /// Call before adding exporters so the processors run first.
    /// </summary>
    /// <typeparam name="TBuilder">Host builder type, e.g. WebApplicationBuilder, HostApplicationBuilder.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <param name="additionalSensitiveAttribute">Optional host policy naming extra attributes to remove; it cannot permit attributes the shared baseline removes.</param>
    /// <returns>The updated builder instance for chaining.</returns>
    public static TBuilder AddTelemetryPrivacy<TBuilder>(this TBuilder builder, Func<string, bool>? additionalSensitiveAttribute = null) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = false;
            logging.IncludeScopes = false;
            logging.AddProcessor(_ => new LogRecordPrivacyProcessor(additionalSensitiveAttribute));
        });
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(new ActivityPrivacyProcessor(additionalSensitiveAttribute)));

        return builder;
    }

    /// <summary>
    /// Adds startup logging for the application name and informational version.
    /// </summary>
    /// <typeparam name="TBuilder">Host builder type, e.g. WebApplicationBuilder, HostApplicationBuilder.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <param name="applicationVersion">The optional application version to log. When omitted, the entry assembly informational version is used.</param>
    /// <returns>The updated builder instance for chaining.</returns>
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
