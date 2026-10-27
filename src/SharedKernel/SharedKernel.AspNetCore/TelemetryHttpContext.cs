using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PandaBear.AspNetCore;

/// <summary>Reads the bounded request facts that telemetry may carry.</summary>
internal static class TelemetryHttpContext
{
    /// <summary>Gets the matched route template, never the raw request path.</summary>
    public static string RouteTemplate(HttpContext httpContext) =>
        // The exception-handler middleware clears the endpoint and keeps the original on its feature.
        (httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint()) is RouteEndpoint { RoutePattern.RawText: { Length: > 0 } template }
            ? template
            : AspNetCoreTelemetry.UnmatchedRoute;

    /// <summary>Gets the trace identifier that ties a log record to its response and trace.</summary>
    public static string TraceId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
