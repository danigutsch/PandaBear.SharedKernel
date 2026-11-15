using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>Counts the page views that were not delivered, by reason. A failing meter or listener never reaches the caller.</summary>
internal sealed class UmamiPageViewMetrics
{
    private Counter<long>? _dropped;

    /// <summary>Initializes the counter from the host's meter factory.</summary>
    /// <param name="meterFactory">The factory that owns the meter.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes the meter.")]
    public UmamiPageViewMetrics(IMeterFactory meterFactory) =>
        TelemetryGuard.Run(() => _dropped = meterFactory.Create(WebAnalyticsTelemetry.MeterName).CreateCounter<long>(
            WebAnalyticsTelemetry.DroppedPageViewsCounterName,
            unit: "{page_view}",
            description: "Page views that were not delivered to Umami, by reason."));

    /// <summary>Counts dropped page views.</summary>
    /// <param name="reason">One of the reason values of <see cref="WebAnalyticsTelemetry"/>.</param>
    /// <param name="count">How many were dropped.</param>
    public void Dropped(string reason, int count = 1) =>
        TelemetryGuard.Run(() => _dropped?.Add(count, new KeyValuePair<string, object?>(WebAnalyticsTelemetry.ReasonTag, reason)));
}
