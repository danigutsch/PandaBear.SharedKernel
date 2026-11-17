using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PandaBear.Observability;

namespace PandaBear.ServiceDefaults;

/// <summary>
/// Aspire service defaults for SharedKernel hosts. They follow the Aspire service-defaults contract (OpenTelemetry, health checks, service
/// discovery and HTTP resilience from one <c>AddServiceDefaults</c> call, and <c>/health</c> plus <c>/alive</c> from one
/// <c>MapDefaultEndpoints</c> call) and route telemetry through the SharedKernel privacy baseline.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>The tag of the health checks that the liveness endpoint runs. Tag a check with it only when a failure means the process must be restarted.</summary>
    public const string LiveTag = "live";

    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string SelfCheckName = "self";

    /// <summary>
    /// Adds the service defaults: for every <see cref="HttpClient"/> the standard resilience handler, changed by
    /// <see cref="ServiceDefaultsOptions.ConfigureResilience"/> and never retrying unsafe HTTP methods, unless
    /// <see cref="ServiceDefaultsOptions.AddResilience"/> is off, then one service discovery handler inside it; OpenTelemetry logs, metrics
    /// and traces through <see cref="ObservabilityBuilderExtensions.ConfigureOpenTelemetry{TBuilder}"/> (privacy processors included) and
    /// every SharedKernel meter and activity source (the <c>SharedKernel.HttpClients</c> outcome source among them), an OTLP exporter only
    /// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, and a liveness check named <c>self</c> tagged <see cref="LiveTag"/>. Requests to
    /// the health and liveness paths are not traced.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="configure">Changes the default settings.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A health path does not start with <c>/</c>, or both paths are the same.</exception>
    /// <remarks>
    /// The privacy processors are registered before the OTLP exporter, so they run first. Exceptions are not recorded on request spans;
    /// their messages can carry personal data. These are the HttpClient defaults of the host: do not add a second resilience or service
    /// discovery handler to every client, which would nest the pipelines.
    /// </remarks>
    public static TBuilder AddSharedKernelServiceDefaults<TBuilder>(this TBuilder builder, Action<ServiceDefaultsOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ServiceDefaultsOptions();
        configure?.Invoke(options);
        if (Problem(options) is { } problem)
        {
            throw new ArgumentException(problem, nameof(configure));
        }

        builder.Services.AddSingleton(options);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            if (options.AddResilience)
            {
                http.AddStandardResilienceHandler(resilience =>
                {
                    options.ConfigureResilience?.Invoke(resilience);
                    resilience.Retry.DisableForUnsafeHttpMethods();
                });
            }

            http.AddServiceDiscovery();
        });

        builder.ConfigureOpenTelemetry();
        var sensitive = options.SensitiveTelemetryAttributes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        builder.AddTelemetryPrivacy(sensitive.Count == 0 ? null : sensitive.Contains);
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSharedKernelInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(instrumentation => instrumentation.Filter = context => !IsHealthRequest(context, options))
                .AddHttpClientInstrumentation()
                .AddSharedKernelInstrumentation());
        builder.Services.PostConfigureAll<AspNetCoreTraceInstrumentationOptions>(static instrumentation => instrumentation.RecordException = false);

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        builder.Services.AddHealthChecks().AddCheck(SelfCheckName, static () => HealthCheckResult.Healthy(), [LiveTag]);
        return builder;
    }

    /// <summary>
    /// Maps the readiness endpoint (<see cref="ServiceDefaultsOptions.HealthPath"/>, every check) and the liveness endpoint
    /// (<see cref="ServiceDefaultsOptions.AlivePath"/>, checks tagged <see cref="LiveTag"/>), as <see cref="ServiceDefaultsOptions.Exposure"/>
    /// allows. They allow anonymous requests, forbid caching and answer only the overall status as plain text (<c>Healthy</c>,
    /// <c>Degraded</c> or <c>Unhealthy</c>), never a check name, an exception or a dependency detail.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="AddSharedKernelServiceDefaults{TBuilder}"/> was not called.</exception>
    /// <remarks>
    /// The default exposure is <see cref="HealthEndpointExposure.Always"/>: an Aspire AppHost's <c>WithHttpHealthCheck("/health")</c> and
    /// <c>WaitFor</c> need the endpoint in whatever environment the project runs, and container orchestrators probe production hosts.
    /// </remarks>
    public static WebApplication MapSharedKernelDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetService<ServiceDefaultsOptions>()
            ?? throw new InvalidOperationException($"Call {nameof(AddSharedKernelServiceDefaults)} on the builder before mapping the default endpoints.");
        if (options.Exposure == HealthEndpointExposure.DevelopmentOnly && !app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapHealthChecks(options.HealthPath, CreateHealthCheckOptions(predicate: null)).AllowAnonymous();
        app.MapHealthChecks(options.AlivePath, CreateHealthCheckOptions(static registration => registration.Tags.Contains(LiveTag))).AllowAnonymous();
        return app;
    }

    private static string? Problem(ServiceDefaultsOptions options)
    {
        if (!IsPath(options.HealthPath) || !IsPath(options.AlivePath))
        {
            return "The health and liveness paths must start with '/' and contain no white space.";
        }

        return string.Equals(options.HealthPath, options.AlivePath, StringComparison.OrdinalIgnoreCase)
            ? "The health and liveness paths must differ."
            : null;
    }

    private static bool IsPath(string? path) => path is { Length: > 1 } && path[0] == '/' && !path.AsSpan().ContainsAnyWhiteSpace();

    private static bool IsHealthRequest(HttpContext context, ServiceDefaultsOptions options) =>
        context.Request.Path.StartsWithSegments(options.HealthPath, StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments(options.AlivePath, StringComparison.OrdinalIgnoreCase);

    private static HealthCheckOptions CreateHealthCheckOptions(Func<HealthCheckRegistration, bool>? predicate) => new()
    {
        AllowCachingResponses = false,
        Predicate = predicate,
        ResponseWriter = WriteStatus,
    };

    private static Task WriteStatus(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(report.Status.ToString(), context.RequestAborted);
    }
}
