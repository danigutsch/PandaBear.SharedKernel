using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed partial class IntegrationEventOutboxRelayHostedService<TContext>(
    EfIntegrationEventOutboxRelay<TContext> relay,
    ILogger<IntegrationEventOutboxRelayHostedService<TContext>> logger,
    IOptions<IntegrationEventOutboxRelayOptions> options)
    : IHostedService
    where TContext : DbContext
{
    private CancellationTokenSource? stoppingTokenSource;

    private Task? executingTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        stoppingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executingTask = Execute(stoppingTokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var task = executingTask;
        var tokenSource = Interlocked.Exchange(ref stoppingTokenSource, null);
        if (task is null || tokenSource is null)
        {
            return;
        }

        try
        {
            try
            {
                await tokenSource.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            tokenSource.Dispose();
        }
    }

    private async Task Execute(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int published;
                do
                {
                    published = await relay.PublishPending(stoppingToken).ConfigureAwait(false);
                }
                while (published > 0);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (ShouldContinueAfterRelayFailure(exception))
            {
                LogOutboxRelayFailure(logger, exception, typeof(TContext).Name);
            }

            await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private static bool ShouldContinueAfterRelayFailure(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not ThreadAbortException;
    }

    [LoggerMessage(1, LogLevel.Error, "Integration event outbox relay failed for DbContext {DbContextName}.")]
    private static partial void LogOutboxRelayFailure(ILogger logger, Exception exception, string dbContextName);
}
