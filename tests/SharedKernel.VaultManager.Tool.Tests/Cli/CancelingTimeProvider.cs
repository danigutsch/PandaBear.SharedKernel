using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>
/// Clock that cancels a command when the merge reads the time, as Ctrl+C or SIGTERM does while the merge runs, away from
/// any password prompt and before the merged vault is written. Timers run on the system clock.
/// </summary>
/// <param name="cancellation">Cancels the command.</param>
internal sealed class CancelingTimeProvider(CancellationTokenSource cancellation) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        cancellation.Cancel();
        return PipelineInputs.Now;
    }
}
