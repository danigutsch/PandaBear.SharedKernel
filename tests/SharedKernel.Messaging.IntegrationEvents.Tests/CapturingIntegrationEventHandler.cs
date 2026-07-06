namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class CapturingIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
    public TestIntegrationEvent? IntegrationEvent { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public ValueTask Handle(TestIntegrationEvent notification, CancellationToken ct)
    {
        IntegrationEvent = notification;
        CancellationToken = ct;

        return ValueTask.CompletedTask;
    }
}
