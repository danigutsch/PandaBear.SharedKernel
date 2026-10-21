using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace PandaBear.Observability.Tests;

internal sealed class PrivacyLogExporter(ConcurrentQueue<CapturedPrivacyLogRecord> records) : BaseExporter<LogRecord>
{
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            var scopeCount = new int[1];
            record.ForEachScope(static (_, count) => count[0]++, scopeCount);
            records.Enqueue(new CapturedPrivacyLogRecord(
                record.Body,
                record.FormattedMessage,
                record.Exception,
                record.Attributes is null ? [] : [.. record.Attributes],
                scopeCount[0]));
        }

        return ExportResult.Success;
    }
}
