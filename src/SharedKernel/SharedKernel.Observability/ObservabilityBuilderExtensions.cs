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
    /// including cross-service instrumentation and explicit stable service identity.
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

        var resourceBuilder = ResourceBuilder.CreateDefault().AddDetector(new ExplicitServiceNameDetector(builder.Environment.ApplicationName));
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
}
