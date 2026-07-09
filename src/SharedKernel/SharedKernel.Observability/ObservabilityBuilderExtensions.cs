using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace SharedKernel.Observability;

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

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });
        builder.Logging.EnableRedaction();
        builder.Services.AddRedaction();

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
