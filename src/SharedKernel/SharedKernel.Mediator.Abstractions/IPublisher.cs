namespace SharedKernel.Mediator;

/// <summary>
/// Publishes notifications to generated notification handlers.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to matching handlers.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification instance to publish.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when publication finishes.</returns>
    ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct)
        where TNotification : INotification;
}
