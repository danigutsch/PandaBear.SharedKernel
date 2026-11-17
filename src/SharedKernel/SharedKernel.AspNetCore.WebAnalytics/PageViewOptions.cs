namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Settings of the page views that every target shares, registered by <see cref="PageViewHostApplicationBuilderExtensions.AddPageViews"/>
/// and checked when the host starts.
/// </summary>
public sealed class PageViewOptions
{
    /// <summary>The largest <see cref="QueueCapacity"/>.</summary>
    public const int MaximumQueueCapacity = 100_000;

    /// <summary>Gets the longest <see cref="ShutdownTimeout"/>.</summary>
    public static TimeSpan MaximumShutdownTimeout { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the host name a page view carries, such as <c>example.com</c>. When <see langword="null"/>, the host of the request is
    /// used, which a client chooses unless the host filters its <c>Host</c> header (<c>AllowedHosts</c>).
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a request with <c>Sec-GPC: 1</c> (Global Privacy Control) or <c>DNT: 1</c> (Do Not Track)
    /// is left out. On by default.
    /// </summary>
    public bool HonorPrivacySignals { get; set; } = true;

    /// <summary>
    /// Gets or sets how many page views wait for each target at most, 1000 by default. A page view that finds a target's queue full is
    /// dropped for that target and counted; it never waits. From 1 to <see cref="MaximumQueueCapacity"/>.
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how long stopping the host waits for the queued page views to be sent, 5 seconds by default. What is not sent by
    /// then is dropped and counted. Greater than zero and at most <see cref="MaximumShutdownTimeout"/>.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
