namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>
/// Names the meter, counter, tags, tag values and log event identifiers of the page views, so a host can alert on lost page views
/// without copying string literals. No measurement or log entry carries visitor data.
/// </summary>
public static class WebAnalyticsTelemetry
{
    /// <summary>The name of the meter, registered by <c>AddInstrumentation</c> through its <c>SharedKernel.*</c> pattern.</summary>
    public const string MeterName = "SharedKernel.AspNetCore.WebAnalytics";

    /// <summary>The counter of page views that were not delivered to a target, tagged with <see cref="TargetTag"/> and <see cref="ReasonTag"/>.</summary>
    public const string DroppedPageViewsCounterName = "sharedkernel.web_analytics.page_views.dropped";

    /// <summary>The tag that carries the <see cref="IPageViewTarget.Name"/> of the target that did not receive the page view.</summary>
    public const string TargetTag = "sharedkernel.web_analytics.target";

    /// <summary>The tag that carries why a page view was dropped.</summary>
    public const string ReasonTag = "sharedkernel.web_analytics.reason";

    /// <summary>The target's queue was full, because the target is slow or unreachable or the site is busier than the queue allows.</summary>
    public const string QueueFullReason = "queue_full";

    /// <summary>The target answered with a status other than success.</summary>
    public const string RejectedReason = "rejected";

    /// <summary>Sending to the target failed or timed out.</summary>
    public const string SendFailedReason = "send_failed";

    /// <summary>The host was stopping: the page view came after the stop began, or was still queued or in flight when the shutdown timeout ended.</summary>
    public const string ShutdownReason = "shutdown";

    /// <summary>The log event identifier of a target's answer other than success; the entry carries the target name and the status code only.</summary>
    public const int SendRejectedEventId = 4201;

    /// <summary>The log event identifier of a send to a target that failed; the entry carries the target name and the exception type name only.</summary>
    public const int SendFailedEventId = 4202;

    /// <summary>The log event identifier of the page views that were not sent before the shutdown timeout; the entry carries their count and the target name.</summary>
    public const int UnsentAtShutdownEventId = 4204;

    /// <summary>The log event identifier, at information level, of a site that records no page views because no target is configured.</summary>
    public const int PageViewsOffEventId = 4205;
}
