namespace PandaBear.Mediator;

/// <summary>
/// Defines the stable telemetry names of the SharedKernel mediator: the meter and activity source that <c>SharedKernel.Mediator</c>
/// creates, and the span, instrument and attribute names that it and the generated dispatch code in consumer assemblies use.
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
    /// Gets the activity name for the request span that <c>ActivityBehavior</c> starts around the pipeline.
    /// </summary>
    public static string ActivityHandle => "mediator.handle";

    /// <summary>
    /// Gets the counter name for total mediator requests.
    /// </summary>
    public static string MetricRequests => "sharedkernel.mediator.requests";

    /// <summary>
    /// Gets the histogram name for mediator request duration.
    /// </summary>
    public static string MetricRequestDuration => "sharedkernel.mediator.request.duration";

    /// <summary>
    /// Gets the counter name for total mediator notifications.
    /// </summary>
    public static string MetricNotifications => "sharedkernel.mediator.notifications";

    /// <summary>
    /// Gets the histogram name for mediator notification duration.
    /// </summary>
    public static string MetricNotificationDuration => "sharedkernel.mediator.notification.duration";

    /// <summary>
    /// Gets the counter name for total mediator stream requests.
    /// </summary>
    public static string MetricStreams => "sharedkernel.mediator.streams";

    /// <summary>
    /// Gets the tag that captures the simple type name of the mediator request, without namespace or generic arity.
    /// </summary>
    public static string TagRequestName => "sharedkernel.mediator.request.name";

    /// <summary>
    /// Gets the tag that captures the simple type name of the mediator response, without namespace or generic arity.
    /// </summary>
    public static string TagResponseName => "sharedkernel.mediator.response.name";

    /// <summary>
    /// Gets the tag that captures the mediator request assembly name.
    /// </summary>
    public static string TagRequestAssembly => "sharedkernel.mediator.request.assembly";

    /// <summary>
    /// Gets the tag that captures the mediator handler name.
    /// </summary>
    public static string TagHandlerName => "sharedkernel.mediator.handler.name";

    /// <summary>
    /// Gets the tag that captures the mediator pipeline depth.
    /// </summary>
    public static string TagPipelineDepth => "sharedkernel.mediator.pipeline.depth";

    /// <summary>
    /// Gets the tag that captures the notification name.
    /// </summary>
    public static string TagNotificationName => "sharedkernel.mediator.notification.name";

    /// <summary>
    /// Gets the tag that captures the notification assembly name.
    /// </summary>
    public static string TagNotificationAssembly => "sharedkernel.mediator.notification.assembly";

    /// <summary>
    /// Gets the tag that captures the number of notification handlers.
    /// </summary>
    public static string TagNotificationHandlerCount => "sharedkernel.mediator.notification.handler_count";

    /// <summary>
    /// Gets the tag that captures the dispatch outcome: <see cref="OutcomeSuccess" />, <see cref="OutcomeCancelled" /> or
    /// <see cref="OutcomeError" />, on generated spans, generated metrics and <c>ActivityBehavior</c> spans alike.
    /// </summary>
    public static string TagOutcome => "sharedkernel.mediator.outcome";

    /// <summary>
    /// Gets the tag that captures the full type name of the exception that ended the dispatch.
    /// </summary>
    public static string TagErrorType => "error.type";

    /// <summary>
    /// Gets the success outcome value.
    /// </summary>
    public static string OutcomeSuccess => "success";

    /// <summary>
    /// Gets the cancelled outcome value, for an operation stopped because the caller cancelled it.
    /// </summary>
    public static string OutcomeCancelled => "cancelled";

    /// <summary>
    /// Gets the error outcome value, for any other exception.
    /// </summary>
    public static string OutcomeError => "error";
}
