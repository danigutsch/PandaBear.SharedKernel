namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Names the meter, counter, tag values and log event identifiers of the page-view forwarder, so a host can alert on lost page views
/// without copying string literals. No measurement or log entry carries visitor data.
/// </summary>
public static class WebAnalyticsTelemetry
{
    /// <summary>The name of the meter, registered by <c>AddSharedKernelInstrumentation</c> through its <c>PandaBear.*</c> pattern.</summary>
    public const string MeterName = "PandaBear.AspNetCore.WebAnalytics";

    /// <summary>The counter of page views that were not delivered to Umami, tagged with <see cref="ReasonTag"/>.</summary>
    public const string DroppedPageViewsCounterName = "web_analytics_page_views_dropped_total";

    /// <summary>The tag that carries why a page view was dropped.</summary>
    public const string ReasonTag = "reason";

    /// <summary>The queue was full, because Umami is slow or unreachable or the site is busier than the queue allows.</summary>
    public const string QueueFullReason = "queue_full";

    /// <summary>Umami answered with a status other than success.</summary>
    public const string RejectedReason = "rejected";

    /// <summary>The request to Umami failed or timed out.</summary>
    public const string SendFailedReason = "send_failed";

    /// <summary>The host was stopping: the page view came after the stop began, or was still queued or in flight when the shutdown timeout ended.</summary>
    public const string ShutdownReason = "shutdown";

    /// <summary>The log event identifier of an Umami answer other than success; the entry carries the status code only.</summary>
    public const int SendRejectedEventId = 4201;

    /// <summary>The log event identifier of a request to Umami that failed; the entry carries the exception type name only.</summary>
    public const int SendFailedEventId = 4202;

    /// <summary>The log event identifier of a website id lookup that threw; the entry carries the exception type name only.</summary>
    public const int WebsiteIdLookupFailedEventId = 4203;

    /// <summary>The log event identifier of the page views that were not sent before the shutdown timeout; the entry carries their count.</summary>
    public const int UnsentAtShutdownEventId = 4204;
}
