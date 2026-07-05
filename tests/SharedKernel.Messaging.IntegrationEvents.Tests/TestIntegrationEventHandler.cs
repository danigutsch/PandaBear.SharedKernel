namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
    public ValueTask Handle(TestIntegrationEvent notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}
