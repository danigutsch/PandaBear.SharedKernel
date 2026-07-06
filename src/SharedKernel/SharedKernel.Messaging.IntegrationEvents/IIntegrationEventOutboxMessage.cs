namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Exposes durable outbox lifecycle metadata for an integration event message.
/// </summary>
public interface IIntegrationEventOutboxMessage : IRetryableMessage
{
    /// <summary>
    /// Gets the outbox message identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets when the message was enqueued.
    /// </summary>
    DateTimeOffset EnqueuedAt { get; }

    /// <summary>
    /// Gets when the message was published.
    /// </summary>
    DateTimeOffset? PublishedAt { get; }

    /// <summary>
    /// Gets the number of failed publish attempts.
    /// </summary>
    int PublishAttempts { get; }

    /// <summary>
    /// Gets when publication was last attempted.
    /// </summary>
    DateTimeOffset? LastPublishAttemptAt { get; }

    /// <summary>
    /// Gets when the message may be retried.
    /// </summary>
    DateTimeOffset? NextPublishAttemptAt { get; }

    /// <summary>
    /// Gets the last publication error, if any.
    /// </summary>
    string? LastPublishError { get; }
}
