using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace PandaBear.Observability.Tests;

/// <summary>Exporter that copies every exported activity into a queue so tests can inspect the post-processing state.</summary>
/// <param name="activities">Queue receiving each exported activity.</param>
internal sealed class PrivacyActivityExporter(ConcurrentQueue<Activity> activities) : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            activities.Enqueue(activity);
        }

        return ExportResult.Success;
    }
}
