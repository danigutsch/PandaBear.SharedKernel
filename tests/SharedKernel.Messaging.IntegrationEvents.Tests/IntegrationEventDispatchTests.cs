using SharedKernel.Mediator;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

public sealed class IntegrationEventDispatchTests
{
    [Fact]
    public async Task Dispatch_forwards_integration_events_to_the_mediator_publisher()
    {
        // Arrange
        var publisher = new CapturingPublisher();
        var dispatcher = new MediatorIntegrationEventDispatcher(publisher);
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "tour-created");
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await dispatcher.Dispatch(integrationEvent, cancellationTokenSource.Token);

        // Assert
        (publisher.Notification).ShouldBeSameAs(integrationEvent);
        publisher.CancellationToken.ShouldBe(cancellationTokenSource.Token);
    }

    [Fact]
    public void Constructor_rejects_null_publisher()
    {
        // Arrange
        var constructor = typeof(MediatorIntegrationEventDispatcher).GetConstructor([typeof(IPublisher)]).ShouldNotBeNull();

        // Act
        var argumentException = ExceptionAssertions.ThrowsInner<ArgumentNullException>(() => constructor.Invoke([null]));
        argumentException.ParamName.ShouldBe("publisher");
    }

    [Fact]
    public void Dispatch_rejects_null_integration_events()
    {
        // Arrange
        var dispatcher = new MediatorIntegrationEventDispatcher(new CapturingPublisher());
        var method = typeof(MediatorIntegrationEventDispatcher).GetMethod(nameof(MediatorIntegrationEventDispatcher.Dispatch)).ShouldNotBeNull();
        var genericMethod = method.MakeGenericMethod(typeof(TestIntegrationEvent));

        // Act
        var argumentException = ExceptionAssertions.ThrowsInner<ArgumentNullException>(() => genericMethod.Invoke(dispatcher, [null, CancellationToken.None]));
        argumentException.ParamName.ShouldBe("integrationEvent");
    }

    [Fact]
    public void Integration_event_handlers_are_mediator_notification_handlers()
    {
        TestIntegrationEventHandler handler = new();

        (handler).ShouldBeAssignableTo<INotificationHandler<TestIntegrationEvent>>();
    }

    [Fact]
    public void Test_integration_event_exposes_expected_metadata()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        // Act
        var integrationEvent = new TestIntegrationEvent(eventId, occurredAt, "tour-created");

        // Assert
        integrationEvent.EventId.ShouldBe(eventId);
        integrationEvent.OccurredAt.ShouldBe(occurredAt);
        integrationEvent.Name.ShouldBe("tour-created");
        TestIntegrationEvent.EventType.ShouldBe("admin.tour.created");
        TestIntegrationEvent.EventVersion.ShouldBe(1);
    }

}
