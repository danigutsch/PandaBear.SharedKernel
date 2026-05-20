namespace SharedKernel.Mediator;

/// <summary>
/// Handles a published notification.
/// </summary>
/// <typeparam name="TNotification">The notification type handled by the handler.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the provided notification.
    /// </summary>
    /// <param name="notification">The notification instance to handle.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when handling finishes.</returns>
    ValueTask Handle(TNotification notification, CancellationToken ct);
}
