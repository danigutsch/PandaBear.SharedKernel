using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore;

/// <summary>Logs and counts authentication and rate-limit events using only bounded values.</summary>
internal sealed partial class SecurityEventReporter
{
    private readonly ILogger<SecurityEventReporter> _logger;
    private readonly Counter<long> _authenticationEvents;
    private readonly Counter<long> _rateLimitRejections;

    /// <summary>Initializes the reporter and creates its instruments.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="meterFactory">The factory that owns and disposes the meter.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes the meter.")]
    public SecurityEventReporter(ILogger<SecurityEventReporter> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        var meter = CreateMeter(meterFactory);
        _authenticationEvents = meter.CreateCounter<long>(
            AspNetCoreTelemetry.AuthenticationEventsCounterName,
            unit: "{event}",
            description: "JWT bearer authentication failures, challenges and forbidden results.");
        _rateLimitRejections = meter.CreateCounter<long>(
            AspNetCoreTelemetry.RateLimitRejectionsCounterName,
            unit: "{request}",
            description: "Requests rejected by a rate-limit policy.");
    }

    /// <summary>Logs and counts an authentication event.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="securityEvent">The bounded event name.</param>
    /// <param name="reason">The bounded reason.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    public void ReportAuthentication(HttpContext httpContext, string securityEvent, string reason, string scheme)
    {
        LogAuthenticationEvent(
            _logger,
            securityEvent,
            reason,
            scheme,
            TelemetryHttpContext.RouteTemplate(httpContext),
            TelemetryHttpContext.TraceId(httpContext));
        _authenticationEvents.Add(
            1,
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.EventTag, securityEvent),
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.ReasonTag, reason),
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.SchemeTag, scheme));
    }

    /// <summary>Logs and counts a rate-limit rejection.</summary>
    /// <param name="httpContext">The rejected request.</param>
    /// <param name="policy">The application-owned policy name.</param>
    public void ReportRateLimitRejection(HttpContext httpContext, string policy)
    {
        LogRateLimitRejection(
            _logger,
            policy,
            httpContext.Response.StatusCode,
            TelemetryHttpContext.RouteTemplate(httpContext),
            TelemetryHttpContext.TraceId(httpContext));
        _rateLimitRejections.Add(1, new KeyValuePair<string, object?>(AspNetCoreTelemetry.PolicyTag, policy));
    }

    [LoggerMessage(
        EventId = AspNetCoreTelemetry.AuthenticationEventId,
        EventName = "AuthenticationEvent",
        Level = LogLevel.Warning,
        Message = "Authentication event {SecurityEvent} with reason {Reason} on scheme {Scheme}, route {RouteTemplate} and trace {TraceId}")]
    private static partial void LogAuthenticationEvent(ILogger logger, string securityEvent, string reason, string scheme, string routeTemplate, string traceId);

    [LoggerMessage(
        EventId = AspNetCoreTelemetry.RateLimitRejectionEventId,
        EventName = "RateLimitRejection",
        Level = LogLevel.Warning,
        Message = "Rate limit policy {Policy} rejected a request with status {StatusCode} on route {RouteTemplate} and trace {TraceId}")]
    private static partial void LogRateLimitRejection(ILogger logger, string policy, int statusCode, string routeTemplate, string traceId);

    // IMeterFactory owns the meter and disposes it with the service provider.
    private static Meter CreateMeter(IMeterFactory meterFactory) => meterFactory.Create(AspNetCoreTelemetry.MeterName);
}
