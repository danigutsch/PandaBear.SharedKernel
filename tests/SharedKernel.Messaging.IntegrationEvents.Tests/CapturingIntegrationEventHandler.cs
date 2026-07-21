namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class CapturingIntegrationEventHandler :
    IIntegrationEventHandler<TestIntegrationEvent>,
    IIntegrationEventHandler<TestUpdatedIntegrationEvent>,
    IDisposable
{
    public TestIntegrationEvent? IntegrationEvent { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public TestUpdatedIntegrationEvent? UpdatedIntegrationEvent { get; private set; }

    public int InvocationCount { get; private set; }

    public int DisposeCount { get; private set; }

    public bool ThrowWhenCancelled { get; set; }

    public bool IsDisposed { get; private set; }

    public ValueTask Handle(TestIntegrationEvent integrationEvent, CancellationToken ct)
    {
        IntegrationEvent = integrationEvent;
        CancellationToken = ct;
        InvocationCount++;
        if (ThrowWhenCancelled)
        {
            ct.ThrowIfCancellationRequested();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(TestUpdatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        UpdatedIntegrationEvent = integrationEvent;
        CancellationToken = ct;
        InvocationCount++;
        if (ThrowWhenCancelled)
        {
            ct.ThrowIfCancellationRequested();
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        DisposeCount++;
        IsDisposed = true;
    }
}
