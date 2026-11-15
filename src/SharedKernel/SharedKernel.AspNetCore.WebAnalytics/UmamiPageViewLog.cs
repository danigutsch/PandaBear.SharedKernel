using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>Log messages of the page-view forwarder. None carries visitor data: no address, browser, path, query, referrer or language.</summary>
internal static partial class UmamiPageViewLog
{
    /// <summary>Logs that Umami answered a page view with a status other than success.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="statusCode">The status code of the answer.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.SendRejectedEventId, Level = LogLevel.Warning, Message = "Umami answered a page view with status {StatusCode}; the page view is dropped.")]
    public static partial void SendRejected(ILogger logger, int statusCode);

    /// <summary>Logs that sending a page view to Umami failed.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="errorType">The type name of the exception, never its message.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.SendFailedEventId, Level = LogLevel.Warning, Message = "Sending a page view to Umami failed with {ErrorType}; the page view is dropped.")]
    public static partial void SendFailed(ILogger logger, string errorType);

    /// <summary>Logs that the application's website id lookup threw.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="errorType">The type name of the exception, never its message.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.WebsiteIdLookupFailedEventId, Level = LogLevel.Warning, Message = "The Umami website id lookup failed with {ErrorType}; the page view is not recorded.")]
    public static partial void WebsiteIdLookupFailed(ILogger logger, string errorType);

    /// <summary>Logs how many page views were not sent before the shutdown timeout.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="count">How many page views were dropped.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.UnsentAtShutdownEventId, Level = LogLevel.Warning, Message = "{Count} page views were not sent to Umami before the shutdown timeout and are dropped.")]
    public static partial void UnsentAtShutdown(ILogger logger, int count);
}
