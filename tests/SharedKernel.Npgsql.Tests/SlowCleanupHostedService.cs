using Microsoft.Extensions.Hosting;

namespace PandaBear.Npgsql.Tests;

/// <summary>A hosted service whose shutdown takes 35 seconds and records that it finished, to show teardown waits longer than the generic host's default 30 seconds.</summary>
internal sealed class SlowCleanupHostedService : IHostedService
{
    /// <summary>Gets a value indicating whether the slow shutdown ran to completion.</summary>
    public bool Completed
    {
        get; private set;
    }

    /// <summary>Does nothing on start.</summary>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Waits 35 seconds, honoring cancellation, and then marks the shutdown as completed.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Exercise real cancellation beyond the generic host's 30-second shutdown budget.
        await Task.Delay(TimeSpan.FromSeconds(35), cancellationToken);
        Completed = true;
    }
}
