namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Aggregates unpublished outbox messages and their retry backlog.</summary>
/// <param name="PendingCount">Number of unpublished messages, including retries and claimed messages.</param>
/// <param name="OldestEnqueuedAt">Earliest enqueue timestamp among unpublished messages, when present.</param>
/// <param name="RetryCount">Number of unpublished messages with a persisted next retry timestamp.</param>
/// <param name="OldestRetryEnqueuedAt">Earliest enqueue timestamp among retries, when present.</param>
/// <param name="DueRetryCount">Number of retries scheduled at or before the supplied cutoff.</param>
public readonly record struct IntegrationEventOutboxHealth(
    long PendingCount,
    DateTimeOffset? OldestEnqueuedAt,
    long RetryCount,
    DateTimeOffset? OldestRetryEnqueuedAt,
    long DueRetryCount);
