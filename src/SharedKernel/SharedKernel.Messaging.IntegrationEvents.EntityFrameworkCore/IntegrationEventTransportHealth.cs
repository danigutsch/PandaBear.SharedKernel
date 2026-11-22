namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Aggregates a consumer's durable terminal failures and retry backlog.</summary>
/// <param name="TerminalCount">Number of poisoned messages for the selected consumer.</param>
/// <param name="OldestPoisonedAt">Earliest terminal failure timestamp, when present.</param>
/// <param name="RetryCount">Number of unprocessed, unpoisoned messages with a failed attempt and a persisted next retry timestamp.</param>
/// <param name="OldestRetryReceivedAt">Earliest receipt timestamp among retries, when present.</param>
/// <param name="DueRetryCount">Number of retries scheduled at or before the supplied cutoff.</param>
public readonly record struct IntegrationEventTransportHealth(
    long TerminalCount,
    DateTimeOffset? OldestPoisonedAt,
    long RetryCount,
    DateTimeOffset? OldestRetryReceivedAt,
    long DueRetryCount);
