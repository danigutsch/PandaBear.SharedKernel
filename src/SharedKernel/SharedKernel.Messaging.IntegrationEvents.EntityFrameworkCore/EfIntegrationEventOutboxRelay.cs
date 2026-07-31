using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Publishes pending integration-event outbox messages through a transport-neutral envelope publisher.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
internal sealed class EfIntegrationEventOutboxRelay<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptionsMonitor<IntegrationEventOutboxRelayOptions> options)
    where TContext : DbContext
{
    public async ValueTask<int> PublishPending(CancellationToken ct) =>
        await PublishPending(options.Get(IntegrationEventOptionsNames.Relay<TContext>()).BatchSize, ct).ConfigureAwait(false);

    internal async ValueTask<int> PublishPending(int batchSize, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var scope = scopeFactory.CreateAsyncScope();
        await using var scopeDisposal = scope.ConfigureAwait(false);
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredKeyedService<IEventEnvelopePublisher>(typeof(TContext));
        var claimStrategy = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutboxClaimStrategy<TContext>>();
        var now = timeProvider.GetUtcNow();
        var claimedBy = Guid.CreateVersion7().ToString("N");
        var claimedUntil = now.Add(options.Get(IntegrationEventOptionsNames.Relay<TContext>()).ClaimLeaseDuration);
        var messages = await claimStrategy.ClaimPending(dbContext, batchSize, now, claimedBy, claimedUntil, ct)
            .ConfigureAwait(false);

        if (messages.Length == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await publisher.Publish(message, ct).ConfigureAwait(false);
                message.MarkPublished(timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (ShouldRecordPublishFailure(exception, ct))
            {
                var attemptedAt = timeProvider.GetUtcNow();
                message.MarkPublishFailed(
                    attemptedAt,
                    attemptedAt.Add(GetRetryDelay(message.PublishAttempts)),
                    FormatPublishError(exception));
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return messages.Length;
    }

    private static TimeSpan GetRetryDelay(int failedAttempts)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Min(failedAttempts, 8)));

        return TimeSpan.FromSeconds(seconds);
    }

    private static bool ShouldRecordPublishFailure(Exception exception, CancellationToken ct)
    {
        return exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not ThreadAbortException
            && (exception is not OperationCanceledException || !ct.IsCancellationRequested);
    }

    private static string FormatPublishError(Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().FullName ?? exception.GetType().Name
            : exception.Message;

        return message.Length <= IntegrationEventOutboxMessage.LastPublishErrorMaxLength
            ? message
            : message[..IntegrationEventOutboxMessage.LastPublishErrorMaxLength];
    }
}
