using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace PandaBear.Observability.Tests;

internal sealed class PrivacyLogTestScope : IDisposable
{
    private readonly IHost host;

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

    public ConcurrentQueue<CapturedPrivacyLogRecord> Records { get; } = new();

    public ILogger Logger
    {
        get;
    }

    public void Dispose() => host.Dispose();
}
