using Microsoft.Extensions.Logging;

namespace PandaBear.Scheduling.Tests;

/// <summary>Records the scope and outcome of the first drain cycle.</summary>
/// <param name="logger">Records the polling loop's failure event.</param>
/// <param name="executeBatch">Executes the selected batch number with the host token.</param>
internal sealed class DrainCycleProbe(
    ILogger logger,
    Func<int, CancellationToken, ValueTask<int>> executeBatch)
    : PollingBackgroundService(logger, "drain-cycle-probe", TimeSpan.FromHours(1))
{
    private readonly TaskCompletionSource<Exception?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the first cycle's outcome, with null for a successful drain.</summary>
    public Task<Exception?> Completion => completion.Task;

    /// <summary>Gets the number of drain cycles entered.</summary>
    public int CycleCalls { get; private set; }

    /// <summary>Gets the number of batches entered.</summary>
    public int BatchCalls { get; private set; }

    protected override async ValueTask ExecuteDrainCycle(CancellationToken stoppingToken)
    {
        CycleCalls++;
        try
        {
            await base.ExecuteDrainCycle(stoppingToken);
            completion.SetResult(null);
        }
        catch (Exception exception)
        {
            completion.SetResult(exception);
            throw;
        }
    }

    protected override ValueTask<int> ExecuteBatch(CancellationToken stoppingToken) =>
        executeBatch(++BatchCalls, stoppingToken);
}
