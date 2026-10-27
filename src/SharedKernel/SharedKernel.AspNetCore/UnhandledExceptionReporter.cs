using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore;

/// <summary>
/// Logs and counts an unhandled exception using only bounded facts: the exception type name, the route template, the
/// status code and the trace identifier. The exception message, stack and object never reach the log or the counter.
/// </summary>
/// <remarks>
/// <see cref="AspNetCoreServiceCollectionExtensions.AddUnhandledExceptionHandler" /> registers this reporter. Another
/// host component, such as a Blazor circuit error handler, can resolve it and call <see cref="Report(Exception, string?, int, string?, bool)" />
/// with <c>handled</c> set to <see langword="true" /> after it recovered.
/// </remarks>
public sealed partial class UnhandledExceptionReporter
{
    private readonly ILogger<UnhandledExceptionReporter> _logger;
    private readonly Counter<long> _counter;

    /// <summary>Initializes a new instance of the <see cref="UnhandledExceptionReporter" /> class.</summary>
    /// <param name="logger">The logger that receives the single event per exception.</param>
    /// <param name="meterFactory">The factory that creates the package meter.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes the meter.")]
    public UnhandledExceptionReporter(ILogger<UnhandledExceptionReporter> logger, IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(meterFactory);

        _logger = logger;
        _counter = CreateMeter(meterFactory)
            .CreateCounter<long>(
                AspNetCoreTelemetry.UnhandledExceptionsCounterName,
                unit: "{exception}",
                description: "Unhandled exceptions by exception type and route template.");
    }

    /// <summary>Reports an exception raised while serving an HTTP request.</summary>
    /// <param name="httpContext">The request that raised the exception.</param>
    /// <param name="exception">The exception; only its type name is used.</param>
    /// <param name="handled">Whether a handler produced a response for the exception.</param>
    public void Report(HttpContext httpContext, Exception exception, bool handled)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        Report(
            exception,
            TelemetryHttpContext.RouteTemplate(httpContext),
            httpContext.Response.StatusCode,
            TelemetryHttpContext.TraceId(httpContext),
            handled);
    }

    /// <summary>Reports an exception without an HTTP request, for example one raised inside a Blazor circuit.</summary>
    /// <param name="exception">The exception; only its type name is used.</param>
    /// <param name="routeTemplate">The route template, or <see langword="null" /> when unknown.</param>
    /// <param name="statusCode">The status code of the response, or zero when there is none.</param>
    /// <param name="traceId">The trace identifier, or <see langword="null" /> when unknown.</param>
    /// <param name="handled">Whether a handler recovered from the exception.</param>
    public void Report(Exception exception, string? routeTemplate, int statusCode, string? traceId, bool handled)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errorType = exception.GetType().FullName ?? exception.GetType().Name;
        var route = string.IsNullOrWhiteSpace(routeTemplate) ? AspNetCoreTelemetry.UnmatchedRoute : routeTemplate;

        LogUnhandledException(_logger, errorType, route, statusCode, traceId ?? string.Empty);
        _counter.Add(
            1,
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.ErrorTypeTag, errorType),
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.RouteTemplateTag, route),
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.HandledTag, handled));
    }

    [LoggerMessage(
        EventId = AspNetCoreTelemetry.UnhandledExceptionEventId,
        EventName = "UnhandledException",
        Level = LogLevel.Error,
        Message = "Unhandled exception {ErrorType} on route {RouteTemplate} with status {StatusCode} and trace {TraceId}")]
    private static partial void LogUnhandledException(ILogger logger, string errorType, string routeTemplate, int statusCode, string traceId);

    // IMeterFactory owns the meter and disposes it with the service provider.
    private static Meter CreateMeter(IMeterFactory meterFactory) => meterFactory.Create(AspNetCoreTelemetry.MeterName);
}
