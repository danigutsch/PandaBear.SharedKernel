using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Counts the page views that were not delivered, by target and reason. A failing meter or listener never reaches the caller.</summary>
internal sealed class PageViewMetrics
{
    private Counter<long>? _dropped;

    /// <summary>Initializes the counter from the host's meter factory.</summary>
    /// <param name="meterFactory">The factory that owns the meter.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes the meter.")]
    public PageViewMetrics(IMeterFactory meterFactory) =>
        TelemetryGuard.Run(() => _dropped = meterFactory.Create(WebAnalyticsTelemetry.MeterName).CreateCounter<long>(
            WebAnalyticsTelemetry.DroppedPageViewsCounterName,
            unit: "{page_view}",
            description: "Page views that were not delivered to a target, by target and reason."));

    /// <summary>Counts dropped page views.</summary>
    /// <param name="target">The name of the target that did not receive them.</param>
    /// <param name="reason">One of the reason values of <see cref="WebAnalyticsTelemetry"/>.</param>
    /// <param name="count">How many were dropped.</param>
    public void Dropped(string target, string reason, int count = 1) =>
        TelemetryGuard.Run(() => _dropped?.Add(
            count,
            new KeyValuePair<string, object?>(WebAnalyticsTelemetry.TargetTag, target),
            new KeyValuePair<string, object?>(WebAnalyticsTelemetry.ReasonTag, reason)));
}
