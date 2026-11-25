using Microsoft.Extensions.Logging;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Log messages of the page views. None carries visitor data: no address, browser, path, query, referrer or language.</summary>
internal static partial class PageViewLog
{
    /// <summary>Logs that a target answered a page view with a status other than success.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="target">The name of the target.</param>
    /// <param name="statusCode">The status code of the answer.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.SendRejectedEventId, EventName = "PageViewSendRejected", Level = LogLevel.Warning, Message = "The page-view target {Target} answered with status {StatusCode}; the page view is dropped.")]
    public static partial void SendRejected(ILogger logger, string target, int statusCode);

    /// <summary>Logs that sending a page view to a target failed.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="target">The name of the target.</param>
    /// <param name="errorType">The full type name of the exception, never its message.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.SendFailedEventId, EventName = "PageViewSendFailed", Level = LogLevel.Warning, Message = "Sending a page view to the target {Target} failed with {ErrorType}; the page view is dropped.")]
    public static partial void SendFailed(ILogger logger, string target, string errorType);

    /// <summary>Logs how many page views were not sent to a target before the shutdown timeout.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="count">How many page views were dropped.</param>
    /// <param name="target">The name of the target.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.UnsentAtShutdownEventId, EventName = "PageViewsUnsentAtShutdown", Level = LogLevel.Warning, Message = "{Count} page views were not sent to the target {Target} before the shutdown timeout and are dropped.")]
    public static partial void UnsentAtShutdown(ILogger logger, int count, string target);

    /// <summary>Logs that no target is configured, so the site records no page views.</summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(EventId = WebAnalyticsTelemetry.PageViewsOffEventId, EventName = "PageViewsOff", Level = LogLevel.Information, Message = "Page views are off: no page-view target is configured.")]
    public static partial void PageViewsOff(ILogger logger);
}
