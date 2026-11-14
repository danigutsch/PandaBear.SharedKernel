using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace PandaBear.ServiceDefaults.Tests;

/// <summary>Keeps every exported span in memory.</summary>
internal sealed class RecordingActivityExporter : BaseExporter<Activity>
{
    /// <summary>Gets the exported spans.</summary>
    internal ConcurrentQueue<Activity> Exported { get; } = new();

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            Exported.Enqueue(activity);
        }

        return ExportResult.Success;
    }

    /// <summary>Waits until a span with the given route was exported.</summary>
    /// <param name="route">The <c>http.route</c> tag.</param>
    /// <returns>The span.</returns>
    /// <exception cref="TimeoutException">No such span was exported within five seconds.</exception>
    internal async Task<Activity> WaitForRoute(string route)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var span = Exported.FirstOrDefault(activity => Equals(activity.GetTagItem("http.route"), route));
            if (span is not null)
            {
                return span;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No span with the route {route} was exported.");
    }
}
