namespace SharedKernel.Mediator;

/// <summary>
/// Defines the stable telemetry names used by SharedKernel.Mediator.
/// </summary>
public static class MediatorTelemetry
{
    /// <summary>
    /// Gets the shared telemetry source and meter name for mediator dispatch.
    /// </summary>
    public static string Name => "SharedKernel.Mediator";

    /// <summary>
    /// Gets the activity name for generated request dispatch spans.
    /// </summary>
    public static string ActivitySend => "mediator.send";

    /// <summary>
    /// Gets the activity name for generated stream dispatch spans.
    /// </summary>
    public static string ActivityStream => "mediator.stream";

    /// <summary>
    /// Gets the activity name for generated notification publish spans.
    /// </summary>
    public static string ActivityPublish => "mediator.publish";

    /// <summary>
    /// Gets the activity name for generated notification handler spans.
    /// </summary>
    public static string ActivityNotificationHandler => "mediator.notification.handle";

    /// <summary>
    /// Gets the counter name for total mediator requests.
    /// </summary>
    public static string MetricRequests => "mediator.requests";

    /// <summary>
    /// Gets the histogram name for mediator request duration.
    /// </summary>
    public static string MetricRequestDuration => "mediator.request.duration";

    /// <summary>
    /// Gets the counter name for total mediator notifications.
    /// </summary>
    public static string MetricNotifications => "mediator.notifications";

    /// <summary>
    /// Gets the histogram name for mediator notification duration.
    /// </summary>
    public static string MetricNotificationDuration => "mediator.notification.duration";

    /// <summary>
    /// Gets the counter name for total mediator stream requests.
    /// </summary>
    public static string MetricStreams => "mediator.streams";

    /// <summary>
    /// Gets the activity tag that captures the request CLR type for runtime pipeline spans.
    /// </summary>
    public static string TagRequestType => "sharedkernel.mediator.request_type";

    /// <summary>
    /// Gets the activity tag that captures the response CLR type for runtime pipeline spans.
    /// </summary>
    public static string TagResponseType => "sharedkernel.mediator.response_type";

    /// <summary>
    /// Gets the tag that captures the mediator request name.
    /// </summary>
    public static string TagRequestName => "mediator.request.name";

    /// <summary>
    /// Gets the tag that captures the mediator request assembly name.
    /// </summary>
    public static string TagRequestAssembly => "mediator.request.assembly";

    /// <summary>
    /// Gets the tag that captures the mediator handler name.
    /// </summary>
    public static string TagHandlerName => "mediator.handler.name";

    /// <summary>
    /// Gets the tag that captures the mediator pipeline depth.
    /// </summary>
    public static string TagPipelineDepth => "mediator.pipeline.depth";

    /// <summary>
    /// Gets the tag that captures the notification name.
    /// </summary>
    public static string TagNotificationName => "mediator.notification.name";

    /// <summary>
    /// Gets the tag that captures the notification assembly name.
    /// </summary>
    public static string TagNotificationAssembly => "mediator.notification.assembly";

    /// <summary>
    /// Gets the tag that captures the number of notification handlers.
    /// </summary>
    public static string TagNotificationHandlerCount => "mediator.notification.handler.count";

    /// <summary>
    /// Gets the tag that captures request outcome.
    /// </summary>
    public static string TagOutcome => "mediator.outcome";

    /// <summary>
    /// Gets the tag that captures runtime pipeline outcome.
    /// </summary>
    public static string TagRuntimeOutcome => "sharedkernel.mediator.outcome";

    /// <summary>
    /// Gets the tag that captures the exception type.
    /// </summary>
    public static string TagErrorType => "error.type";

    /// <summary>
    /// Gets the success outcome value.
    /// </summary>
    public static string OutcomeSuccess => "success";

    /// <summary>
    /// Gets the cancelled outcome value.
    /// </summary>
    public static string OutcomeCancelled => "cancelled";

    /// <summary>
    /// Gets the error outcome value.
    /// </summary>
    public static string OutcomeError => "error";
}
