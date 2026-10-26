using Microsoft.Extensions.Logging;

namespace PandaBear.Scheduling.Tests;

/// <summary>
/// A polling service whose batches follow a script: each entry is a processed count, or an exception to throw.
/// </summary>
internal sealed class ScriptedPollingService(
    ILogger logger,
    string serviceName,
    TimeSpan pollInterval,
    params object[] script) : PollingBackgroundService(logger, serviceName, pollInterval)
{
    private readonly Queue<object> pending = new(script);
    private readonly TaskCompletionSource scriptConsumed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int BatchCalls
    {
        get; private set;
    }

    public Task ScriptConsumed => scriptConsumed.Task;

    protected override ValueTask<int> ExecuteBatch(CancellationToken stoppingToken)
    {
        BatchCalls++;
        if (!pending.TryDequeue(out var step))
        {
            scriptConsumed.TrySetResult();
            return ValueTask.FromResult(0);
        }

        if (pending.Count == 0)
        {
            scriptConsumed.TrySetResult();
        }

        return step switch
        {
            Exception exception => throw exception,
            int processed => ValueTask.FromResult(processed),
            _ => throw new InvalidOperationException("Script entries are int or Exception."),
        };
    }
}
