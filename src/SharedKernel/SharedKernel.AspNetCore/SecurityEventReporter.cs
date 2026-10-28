using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore;

/// <summary>Logs and counts authentication and rate-limit events using only bounded values.</summary>
internal sealed partial class SecurityEventReporter
{
    private readonly ILogger<SecurityEventReporter> _logger;
    private Counter<long>? _authenticationEvents;
    private Counter<long>? _rateLimitRejections;

    /// <summary>Initializes the reporter and creates its instruments.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="meterFactory">The factory that owns and disposes the meter.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes the meter.")]
    public SecurityEventReporter(ILogger<SecurityEventReporter> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        TelemetryGuard.Run(() =>
        {
            var meter = CreateMeter(meterFactory);
            _authenticationEvents = meter.CreateCounter<long>(
                AspNetCoreTelemetry.AuthenticationEventsCounterName,
                unit: "{event}",
                description: "JWT bearer authentication failures, challenges and forbidden results.");
            _rateLimitRejections = meter.CreateCounter<long>(
                AspNetCoreTelemetry.RateLimitRejectionsCounterName,
                unit: "{request}",
                description: "Requests rejected by a rate-limit policy.");
        });
    }

    /// <summary>Logs and counts an authentication event.</summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="securityEvent">The bounded event name.</param>
    /// <param name="reason">The bounded reason.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    public void ReportAuthentication(HttpContext httpContext, string securityEvent, string reason, string scheme)
    {
        var level = IsRoutineAnonymousTraffic(securityEvent, reason) ? LogLevel.Information : LogLevel.Warning;
        TelemetryGuard.Run(() =>
        {
            var route = TelemetryHttpContext.RouteTemplate(httpContext);
            var trace = TelemetryHttpContext.TraceId(httpContext);
            LogAuthenticationEvent(_logger, level, securityEvent, reason, scheme, route, trace);
        });
        TelemetryGuard.Run(() => _authenticationEvents?.Add(
            1,
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.EventTag, securityEvent),
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.ReasonTag, reason),
            new KeyValuePair<string, object?>(AspNetCoreTelemetry.SchemeTag, scheme)));
    }

    /// <summary>A challenge without a token is routine anonymous traffic; every other event is a failure worth a warning.</summary>
    private static bool IsRoutineAnonymousTraffic(string securityEvent, string reason) =>
        securityEvent == JwtBearerSecurityEventsPostConfigureOptions.Challenge && reason == JwtBearerSecurityEventsPostConfigureOptions.MissingToken;

    /// <summary>Logs and counts a rate-limit rejection.</summary>
    /// <param name="httpContext">The rejected request.</param>
    /// <param name="policy">The application-owned policy name.</param>
    public void ReportRateLimitRejection(HttpContext httpContext, string policy)
    {
        TelemetryGuard.Run(() =>
        {
            var route = TelemetryHttpContext.RouteTemplate(httpContext);
            var trace = TelemetryHttpContext.TraceId(httpContext);
            LogRateLimitRejection(_logger, policy, httpContext.Response.StatusCode, route, trace);
        });
        TelemetryGuard.Run(() => _rateLimitRejections?.Add(1, new KeyValuePair<string, object?>(AspNetCoreTelemetry.PolicyTag, policy)));
    }

    [LoggerMessage(
        EventId = AspNetCoreTelemetry.AuthenticationEventId,
        EventName = "AuthenticationEvent",
        Message = "Authentication event {SecurityEvent} with reason {Reason} on scheme {Scheme}, route {RouteTemplate} and trace {TraceId}")]
    private static partial void LogAuthenticationEvent(ILogger logger, LogLevel level, string securityEvent, string reason, string scheme, string routeTemplate, string traceId);

    [LoggerMessage(
        EventId = AspNetCoreTelemetry.RateLimitRejectionEventId,
        EventName = "RateLimitRejection",
        Level = LogLevel.Warning,
        Message = "Rate limit policy {Policy} rejected a request with status {StatusCode} on route {RouteTemplate} and trace {TraceId}")]
    private static partial void LogRateLimitRejection(ILogger logger, string policy, int statusCode, string routeTemplate, string traceId);

    /// <summary>Creates the meter through the factory, which owns it and disposes it with the service provider.</summary>
    private static Meter CreateMeter(IMeterFactory meterFactory) => meterFactory.Create(AspNetCoreTelemetry.MeterName);
}
