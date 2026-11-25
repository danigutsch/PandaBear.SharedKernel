using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.Mediator;

/// <summary>
/// Holds the activity source and meters for generated mediator dispatch.
/// </summary>
public sealed class AppMediatorInstrumentation : IDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppMediatorInstrumentation" /> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory used to create the mediator meter.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="meterFactory"/> is <see langword="null"/>.</exception>
    public AppMediatorInstrumentation(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _activitySource = MediatorActivitySource.Source;
        _meter = meterFactory.Create(MediatorTelemetry.Name);
        RequestsTotal = _meter.CreateCounter<long>(MediatorTelemetry.MetricRequests, "{request}", "Total mediator requests dispatched.");
        RequestsDuration = _meter.CreateHistogram(MediatorTelemetry.MetricRequestDuration, "s", "Duration of mediator request dispatch.", advice: DurationBuckets.Advice);
        NotificationsTotal = _meter.CreateCounter<long>(MediatorTelemetry.MetricNotifications, "{notification}", "Total mediator notifications dispatched.");
        NotificationsDuration = _meter.CreateHistogram(MediatorTelemetry.MetricNotificationDuration, "s", "Duration of mediator notification fan-out.", advice: DurationBuckets.Advice);
        StreamsTotal = _meter.CreateCounter<long>(MediatorTelemetry.MetricStreams, "{request}", "Total mediator stream requests dispatched.");
    }

    /// <summary>
    /// Gets the shared activity source for mediator dispatch spans.
    /// </summary>
    public ActivitySource ActivitySource => _activitySource;

    /// <summary>
    /// Gets the counter for total mediator requests.
    /// </summary>
    public Counter<long> RequestsTotal { get; }

    /// <summary>
    /// Gets the histogram for mediator request duration, in seconds.
    /// </summary>
    public Histogram<double> RequestsDuration { get; }

    /// <summary>
    /// Gets the counter for total mediator notifications.
    /// </summary>
    public Counter<long> NotificationsTotal { get; }

    /// <summary>
    /// Gets the histogram for mediator notification duration, in seconds.
    /// </summary>
    public Histogram<double> NotificationsDuration { get; }

    /// <summary>
    /// Gets the counter for total mediator stream requests.
    /// </summary>
    public Counter<long> StreamsTotal { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}
