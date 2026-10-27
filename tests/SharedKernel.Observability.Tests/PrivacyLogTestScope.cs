using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace PandaBear.Observability.Tests;

/// <summary>Test host with OpenTelemetry logging, the log privacy processor and a capturing exporter wired in, disposed together.</summary>
internal sealed class PrivacyLogTestScope : IDisposable
{
    private readonly IHost host;

    /// <summary>Builds the host with formatted messages and scopes excluded, the privacy processor using the optional extra policy, and the capturing exporter feeding <see cref="Records"/>.</summary>
    /// <param name="additionalSensitiveAttribute">Optional predicate naming extra attributes to redact, or null for baseline redaction only.</param>
    public PrivacyLogTestScope(Func<string, bool>? additionalSensitiveAttribute = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.ConfigureOpenTelemetry();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = false;
            logging.IncludeScopes = false;
            logging.AddProcessor(_ => new LogRecordPrivacyProcessor(additionalSensitiveAttribute));
            logging.AddProcessor(_ => new SimpleLogRecordExportProcessor(new PrivacyLogExporter(Records)));
        });
        host = builder.Build();
        Logger = host.Services.GetRequiredService<ILogger<LogRecordPrivacyProcessorTests>>();
    }

    /// <summary>Gets the log records that reached the exporter after privacy processing.</summary>
    public ConcurrentQueue<CapturedPrivacyLogRecord> Records { get; } = new();

    /// <summary>Gets a logger resolved from the host whose output flows through the privacy pipeline.</summary>
    public ILogger Logger
    {
        get;
    }

    /// <summary>Disposes the underlying host.</summary>
    public void Dispose() => host.Dispose();
}
