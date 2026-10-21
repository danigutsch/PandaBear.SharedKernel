using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace PandaBear.Observability.Tests;

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
