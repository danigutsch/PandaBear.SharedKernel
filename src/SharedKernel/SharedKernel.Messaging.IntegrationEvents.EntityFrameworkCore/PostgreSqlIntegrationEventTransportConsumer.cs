using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class PostgreSqlIntegrationEventTransportConsumer<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<IntegrationEventOutboxRelayOptions> options,
    string consumerName)
    where TContext : DbContext
{
    public async ValueTask<int> ConsumePending(CancellationToken ct) =>
        await ConsumePending(options.Value.BatchSize, ct).ConfigureAwait(false);

    internal async ValueTask<int> ConsumePending(int batchSize, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        var now = timeProvider.GetUtcNow();
        var claimedBy = Guid.CreateVersion7().ToString("N");
        var claimedUntil = now.Add(options.Value.ClaimLeaseDuration);
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
                message.MarkConsumeFailed(
                    attemptedAt,
                    attemptedAt.Add(GetRetryDelay(message.ConsumeAttempts)),
                    FormatConsumeError(exception));
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

    private static string FormatConsumeError(Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().FullName ?? exception.GetType().Name
            : exception.Message;

        return message.Length <= IntegrationEventTransportMessage.LastConsumeErrorMaxLength
            ? message
            : message[..IntegrationEventTransportMessage.LastConsumeErrorMaxLength];
    }
}
