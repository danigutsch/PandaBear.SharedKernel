namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Exposes transport-neutral retry state for durable integration-message processing.
/// </summary>
public interface IRetryableMessage
{
    /// <summary>
    /// Gets the number of failed attempts.
    /// </summary>
    int Attempts { get; }

    /// <summary>
    /// Gets when processing was last attempted.
    /// </summary>
    DateTimeOffset? LastAttemptAt { get; }

    /// <summary>
    /// Gets when processing may be retried.
    /// </summary>
    DateTimeOffset? NextAttemptAt { get; }

    /// <summary>
    /// Gets the last failure description, if any.
    /// </summary>
    string? LastError { get; }
}
