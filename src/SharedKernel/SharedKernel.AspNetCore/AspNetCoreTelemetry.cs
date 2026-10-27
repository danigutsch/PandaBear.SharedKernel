namespace PandaBear.AspNetCore;

/// <summary>
/// Names the meter, instruments, tags and log event identifiers that the opt-in ASP.NET Core error and security
/// telemetry emits, so consumers can subscribe, alert and filter without copying string literals.
/// </summary>
public static class AspNetCoreTelemetry
{
    /// <summary>The name of the meter that owns every instrument in this package.</summary>
    public const string MeterName = "PandaBear.AspNetCore";

    /// <summary>The counter of unhandled exceptions, tagged with <see cref="ErrorTypeTag" />, <see cref="RouteTemplateTag" /> and <see cref="HandledTag" />.</summary>
    public const string UnhandledExceptionsCounterName = "unhandled_exceptions_total";

    /// <summary>The counter of JWT bearer authentication failures, challenges and forbidden results, tagged with <see cref="EventTag" />, <see cref="ReasonTag" /> and <see cref="SchemeTag" />.</summary>
    public const string AuthenticationEventsCounterName = "authentication_events_total";

    /// <summary>The counter of rate-limit rejections, tagged with <see cref="PolicyTag" />.</summary>
    public const string RateLimitRejectionsCounterName = "rate_limit_rejections_total";

    /// <summary>The tag that carries the exception type name.</summary>
    public const string ErrorTypeTag = "error_type";

    /// <summary>The tag that carries the matched route template, or <see cref="UnmatchedRoute" />.</summary>
    public const string RouteTemplateTag = "route_template";

    /// <summary>The boolean tag that states whether a handler produced a response for the exception.</summary>
    public const string HandledTag = "handled";

    /// <summary>The tag that carries the authentication event: <c>authentication_failed</c>, <c>challenge</c> or <c>forbidden</c>.</summary>
    public const string EventTag = "event";

    /// <summary>The tag that carries a bounded authentication reason such as <c>token_expired</c>; never token, claim or exception text.</summary>
    public const string ReasonTag = "reason";

    /// <summary>The tag that carries the authentication scheme name.</summary>
    public const string SchemeTag = "scheme";

    /// <summary>The tag that carries the application-owned rate-limit policy name, or <see cref="NoPolicy" />.</summary>
    public const string PolicyTag = "policy";

    /// <summary>The route template reported when no endpoint matched the request.</summary>
    public const string UnmatchedRoute = "unmatched";

    /// <summary>The policy reported when a rejection did not come from a named policy.</summary>
    public const string NoPolicy = "none";

    /// <summary>The stable log event identifier of an unhandled exception.</summary>
    public const int UnhandledExceptionEventId = 4001;

    /// <summary>The stable log event identifier of an authentication event.</summary>
    public const int AuthenticationEventId = 4101;

    /// <summary>The stable log event identifier of a rate-limit rejection.</summary>
    public const int RateLimitRejectionEventId = 4102;
}
