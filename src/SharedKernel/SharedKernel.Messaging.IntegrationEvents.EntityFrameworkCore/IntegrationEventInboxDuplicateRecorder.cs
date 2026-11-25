using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Idempotency;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Records completed duplicate deliveries for registered consumer scopes of one PostgreSQL inbox.</summary>
/// <typeparam name="TContext">The context that owns the inbox and its retention.</typeparam>
public sealed class IntegrationEventInboxDuplicateRecorder<TContext>
    where TContext : DbContext
{
    private readonly TContext context;
    private readonly IntegrationEventInboxDuplicateOptions<TContext> options;

    /// <summary>Creates a scoped recorder for the configured inbox and consumer bindings.</summary>
    internal IntegrationEventInboxDuplicateRecorder(TContext context, IntegrationEventInboxDuplicateOptions<TContext> options)
    {
        this.context = context;
        this.options = options;
    }

    /// <summary>Records a duplicate only while the exact inbox operation is completed and retained.</summary>
    /// <param name="inboxScope">The scope used when starting the original inbox operation.</param>
    /// <param name="eventId">The immutable event identifier. The original operation key is its GUID in N format.</param>
    /// <param name="observedAt">When the caller observed the completed delivery skip.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// True when recorded. An unregistered scope, a missing or incomplete operation, or an event already observed for
    /// another operation of the same consumer returns false.
    /// </returns>
    /// <remarks>
    /// Call only after the application's inbox skips a completed delivery. The registration binds the consumer to the
    /// inbox scope. Recording participates in the context's current transaction.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when PostgreSQL or the opt-in mapping is unavailable.</exception>
    public async Task<bool> Record(IdempotencyScope inboxScope, Guid eventId, DateTimeOffset observedAt, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("An integration event identifier is required.", nameof(eventId));
        }

        ct.ThrowIfCancellationRequested();
        var scope = inboxScope.Value;
        var consumerName = options.FindConsumer(scope);
        if (consumerName is null)
        {
            return false;
        }

        var sql = IntegrationEventInboxDuplicateSql.Create(context);
        var command = FormattableStringFactory.Create(
            sql, scope, eventId.ToString("N"), consumerName, eventId, observedAt.ToUniversalTime(),
            nameof(IdempotencyEntryState.Completed), int.MaxValue);
        return await context.Database.ExecuteSqlInterpolatedAsync(command, ct).ConfigureAwait(false) == 1;
    }
}
