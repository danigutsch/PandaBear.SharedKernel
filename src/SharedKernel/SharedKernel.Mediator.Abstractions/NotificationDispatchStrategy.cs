namespace SharedKernel.Mediator;

/// <summary>
/// Selects how a notification should fan out to its handlers.
/// </summary>
public enum NotificationDispatchStrategy
{
    /// <summary>
    /// Publishes handlers one after another and stops on the first exception.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// Publishes handlers concurrently and waits for all of them to complete.
    /// </summary>
    Parallel = 1,
}
