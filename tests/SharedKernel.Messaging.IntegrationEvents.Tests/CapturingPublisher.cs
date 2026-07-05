using SharedKernel.Mediator;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class CapturingPublisher : IPublisher
{
    public object? Notification { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct)
        where TNotification : INotification
    {
        Notification = notification;
        CancellationToken = ct;

        return ValueTask.CompletedTask;
    }
}
