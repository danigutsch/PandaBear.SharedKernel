using Microsoft.Extensions.Logging;

namespace PandaBear.Scheduling.Tests;

/// <summary>
/// A polling service whose batches follow a script of processed counts, exceptions, delays, and controlled operations.
/// A <see cref="TimeSpan" /> delay returns zero after the wait, giving the cycle a known minimum duration.
/// </summary>
internal sealed class ScriptedPollingService(
    ILogger logger,
    string serviceName,
    TimeSpan pollInterval,
    params object[] script) : PollingBackgroundService(logger, serviceName, pollInterval)
{
    private readonly Queue<object> pending = new(script);
    private readonly TaskCompletionSource scriptConsumed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Number of times the batch has been executed.</summary>
    public int BatchCalls { get; private set; }

    /// <summary>Task that completes once the script has been fully consumed.</summary>
    public Task ScriptConsumed => scriptConsumed.Task;

    protected override async ValueTask<int> ExecuteBatch(CancellationToken stoppingToken)
    {
        BatchCalls++;
        if (!pending.TryDequeue(out var step))
        {
            scriptConsumed.TrySetResult();
            return 0;
        }

        if (pending.Count == 0)
        {
            scriptConsumed.TrySetResult();
        }

        switch (step)
        {
            case TimeSpan delay:
                await Task.Delay(delay, CancellationToken.None);
                return 0;
            case int processed:
                return processed;
            case Exception exception:
                throw exception;
            case Func<CancellationToken, ValueTask<int>> execute:
                return await execute(stoppingToken);
            default:
                throw new InvalidOperationException("Script entries must be counts, exceptions, delays, or batch operations.");
        }
    }
}
