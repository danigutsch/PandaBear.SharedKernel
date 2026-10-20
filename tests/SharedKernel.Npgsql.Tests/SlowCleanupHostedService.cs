using Microsoft.Extensions.Hosting;

namespace SharedKernel.Npgsql.Tests;

internal sealed class SlowCleanupHostedService : IHostedService
{
    public bool Completed { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Exercise real cancellation beyond the generic host's 30-second shutdown budget.
        await Task.Delay(TimeSpan.FromSeconds(35), cancellationToken);
        Completed = true;
    }
}
