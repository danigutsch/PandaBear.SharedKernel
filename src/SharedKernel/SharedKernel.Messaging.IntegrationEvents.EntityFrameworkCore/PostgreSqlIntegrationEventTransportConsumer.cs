using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Claims and consumes pending transport messages for one named consumer.
/// </summary>
/// <param name="scopeFactory">Factory for the per-batch service scope.</param>
/// <param name="timeProvider">Clock for claim and retry times.</param>
/// <param name="options">Monitor that supplies the per-context consumer options.</param>
/// <param name="consumerName">Name of the consumer whose messages are handled.</param>
internal sealed class PostgreSqlIntegrationEventTransportConsumer<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptionsMonitor<IntegrationEventOutboxRelayOptions> options,
    string consumerName)
    where TContext : DbContext
{
    /// <summary>Consumes one batch sized by the context's configured consumer options and returns how many messages were handled.</summary>
    public async ValueTask<int> ConsumePending(CancellationToken ct) =>
        await ConsumePending(options.Get(IntegrationEventOptionsNames.Consumer<TContext>()).BatchSize, ct).ConfigureAwait(false);

    /// <summary>Claims up to <paramref name="batchSize" /> due messages, hands them to their handlers, and returns how many were handled.</summary>
    internal async ValueTask<int> ConsumePending(int batchSize, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var scope = scopeFactory.CreateAsyncScope();
        await using var scopeDisposal = scope.ConfigureAwait(false);
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        var now = timeProvider.GetUtcNow();
        var claimedBy = Guid.CreateVersion7().ToString("N");
        var consumerOptions = options.Get(IntegrationEventOptionsNames.Consumer<TContext>());
        var claimedUntil = now.Add(consumerOptions.ClaimLeaseDuration);
        var messages = await PostgreSqlIntegrationEventTransportClaimSql.ClaimPending(
            dbContext,
            consumerName,
            batchSize,
            now,
            claimedBy,
            claimedUntil,
            ct).ConfigureAwait(false);

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await publisher.Publish(message, ct).ConfigureAwait(false);
                message.MarkProcessed(timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (ShouldRecordConsumeFailure(exception, ct))
            {
                var attemptedAt = timeProvider.GetUtcNow();
                if (message.ConsumeAttempts + 1 >= consumerOptions.MaxConsumeAttempts)
                {
                    message.MarkPoisoned(attemptedAt, DescribeConsumeFailure(exception));
                }
                else
                {
                    message.MarkConsumeFailed(
                        attemptedAt,
                        attemptedAt.Add(GetRetryDelay(message.ConsumeAttempts)),
                        DescribeConsumeFailure(exception));
                }
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

    private static bool ShouldRecordConsumeFailure(Exception exception, CancellationToken ct)
    {
        return exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not ThreadAbortException
            && (exception is not OperationCanceledException || !ct.IsCancellationRequested);
    }

    private static string DescribeConsumeFailure(Exception exception)
    {
        return exception.GetType().FullName ?? exception.GetType().Name;
    }
}
