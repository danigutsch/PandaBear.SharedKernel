using Microsoft.AspNetCore.HttpOverrides;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Settings of the Umami page-view forwarder, registered by
/// <see cref="UmamiPageViewServiceCollectionExtensions.AddUmamiPageViews{TWebsiteIdProvider}"/> and checked when the host starts.
/// </summary>
public sealed class UmamiPageViewOptions
{
    /// <summary>The largest <see cref="QueueCapacity"/>.</summary>
    public const int MaximumQueueCapacity = 100_000;

    /// <summary>Gets the longest <see cref="ShutdownTimeout"/>.</summary>
    public static TimeSpan MaximumShutdownTimeout { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the base address of Umami, such as <c>https://analytics.example.com/</c>; page views are posted to <c>api/send</c>
    /// below it. Read from the configuration key passed to <c>AddUmamiPageViews</c>; it must be an absolute http or https address without
    /// user info, query or fragment.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the host name sent as the page's <c>hostname</c>, such as <c>example.com</c>. When <see langword="null"/>, the host of
    /// the request is sent, which a client chooses unless the host filters its <c>Host</c> header (<c>AllowedHosts</c>).
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a request with <c>Sec-GPC: 1</c> (Global Privacy Control) or <c>DNT: 1</c> (Do Not Track)
    /// is left out. On by default.
    /// </summary>
    public bool HonorPrivacySignals { get; set; } = true;

    /// <summary>
    /// Gets or sets the request header that carries the visitor's address to Umami, <c>X-Forwarded-For</c> by default. Umami must read
    /// the address from it: the Umami Aspire resource sets its <c>CLIENT_IP_HEADER</c> to the default.
    /// </summary>
    public string ClientIpHeader { get; set; } = ForwardedHeadersDefaults.XForwardedForHeaderName;

    /// <summary>
    /// Gets or sets how many page views wait for the sender at most, 1000 by default. A page view that finds the queue full is dropped
    /// and counted; it never waits. From 1 to <see cref="MaximumQueueCapacity"/>.
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how long stopping the host waits for the queued page views to be sent, 5 seconds by default. What is not sent by
    /// then is dropped and counted. Greater than zero and at most <see cref="MaximumShutdownTimeout"/>.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
