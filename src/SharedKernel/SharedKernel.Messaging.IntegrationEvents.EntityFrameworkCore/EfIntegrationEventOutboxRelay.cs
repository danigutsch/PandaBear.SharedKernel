using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Publishes pending integration-event outbox messages through a transport-neutral envelope publisher.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
internal sealed class EfIntegrationEventOutboxRelay<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider)
    where TContext : DbContext
{
    private const int DefaultBatchSize = 20;

    public async ValueTask PublishPending(CancellationToken ct) =>
        await PublishPending(DefaultBatchSize, ct).ConfigureAwait(false);

    internal async ValueTask PublishPending(int batchSize, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        var now = timeProvider.GetUtcNow();
        var messages = await dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(message => message.PublishedAt == null
                && (message.NextPublishAttemptAt == null || message.NextPublishAttemptAt <= now))
            .OrderBy(message => message.EnqueuedAt)
            .Take(batchSize)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

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
                    exception.Message);
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
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
}
