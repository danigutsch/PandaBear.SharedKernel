namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// Handler for TestIntegrationEvent and TestUpdatedIntegrationEvent that records what it receives, how often it ran and whether it was disposed asynchronously.
/// </summary>
internal sealed class CapturingIntegrationEventHandler :
    IIntegrationEventHandler<TestIntegrationEvent>,
    IIntegrationEventHandler<TestUpdatedIntegrationEvent>,
    IAsyncDisposable
{
    /// <summary>
    /// Gets the last TestIntegrationEvent handled, or null.
    /// </summary>
    public TestIntegrationEvent? IntegrationEvent
    {
        get; private set;
    }

    /// <summary>
    /// Gets the cancellation token passed to the last Handle call.
    /// </summary>
    public CancellationToken CancellationToken
    {
        get; private set;
    }

    /// <summary>
    /// Gets the last TestUpdatedIntegrationEvent handled, or null.
    /// </summary>
    public TestUpdatedIntegrationEvent? UpdatedIntegrationEvent
    {
        get; private set;
    }

    /// <summary>
    /// Gets how many events either Handle overload has processed.
    /// </summary>
    public int InvocationCount
    {
        get; private set;
    }

    /// <summary>
    /// Gets how many times DisposeAsync was called.
    /// </summary>
    public int DisposeAsyncCount
    {
        get; private set;
    }

    /// <summary>
    /// Gets or sets whether Handle throws OperationCanceledException when its token is already cancelled.
    /// </summary>
    public bool ThrowWhenCancelled
    {
        get; set;
    }

    /// <summary>
    /// Gets whether DisposeAsync was called at least once.
    /// </summary>
    public bool IsDisposedAsynchronously
    {
        get; private set;
    }

    /// <summary>
    /// Records the TestIntegrationEvent, token and invocation, and throws when cancelled if ThrowWhenCancelled is set.
    /// </summary>
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

    /// <summary>
    /// Records the TestUpdatedIntegrationEvent, token and invocation, and throws when cancelled if ThrowWhenCancelled is set.
    /// </summary>
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

    /// <summary>
    /// Counts the call and marks the handler as asynchronously disposed.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        DisposeAsyncCount++;
        IsDisposedAsynchronously = true;
        return ValueTask.CompletedTask;
    }
}
