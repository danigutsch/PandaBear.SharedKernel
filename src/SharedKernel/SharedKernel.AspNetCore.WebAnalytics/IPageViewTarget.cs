namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>
/// A destination of page views, such as Umami. Register it with <see cref="PageViewBuilder.AddTarget{TTarget}"/> or
/// <see cref="PageViewBuilder.AddTarget(Func{IServiceProvider, IPageViewTarget})"/> only when its own setting is present, so a site
/// without that setting captures and sends nothing for it.
/// </summary>
/// <remarks>
/// Each target has its own bounded queue and its own background loop, which calls <see cref="Send"/> with one page view at a time, in
/// order, so a slow or failing target delays or loses only its own page views. Throw to report a failure: an
/// <see cref="HttpRequestException"/> with a <see cref="HttpRequestException.StatusCode"/>, such as the one
/// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> throws, is counted as <see cref="WebAnalyticsTelemetry.RejectedReason"/>
/// and logged with the status code; any other exception is counted as <see cref="WebAnalyticsTelemetry.SendFailedReason"/> and logged
/// with its type name only, never its message. Do not log the page view or put it in an exception message: it carries the visitor's
/// address and <c>User-Agent</c>.
/// </remarks>
public interface IPageViewTarget
{
    /// <summary>
    /// Gets the name that log entries and the <see cref="WebAnalyticsTelemetry.TargetTag"/> of dropped page views carry, such as
    /// <c>umami</c>. It is unique among the targets of a host, stable, and never personal data.
    /// </summary>
    string Name { get; }

    /// <summary>Sends one page view.</summary>
    /// <param name="pageView">The page view.</param>
    /// <param name="ct">The token that is cancelled when the stopping host gives up sending.</param>
    /// <returns>A task that completes when the target has accepted the page view.</returns>
    Task Send(PageView pageView, CancellationToken ct);
}
