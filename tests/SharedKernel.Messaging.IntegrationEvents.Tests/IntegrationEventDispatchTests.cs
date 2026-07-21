namespace SharedKernel.Messaging.IntegrationEvents.Tests;

public sealed class IntegrationEventDispatchTests
{
    [Fact]
    public void Integration_event_contracts_do_not_depend_on_mediator()
    {
        // Arrange
        var assembly = typeof(IIntegrationEvent).Assembly;
        var referencedAssemblyNames = assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToArray();
        var typeNames = assembly.GetTypes()
            .Select(static type => type.Name)
            .ToArray();

        // Act
        var integrationEventInterfaces = typeof(IIntegrationEvent).GetInterfaces()
            .Select(static type => type.FullName)
            .ToArray();
        var handlerInterfaces = typeof(IIntegrationEventHandler<TestIntegrationEvent>).GetInterfaces()
            .Select(static type => type.FullName)
            .ToArray();

        // Assert
        referencedAssemblyNames.ShouldNotContain("SharedKernel.Mediator.Abstractions");
        typeNames.ShouldNotContain("IIntegrationEventDispatcher");
        typeNames.ShouldNotContain("MediatorIntegrationEventDispatcher");
        typeNames.ShouldNotContain("IIntegrationEventConsumerRegistration");
        typeNames.ShouldNotContain("IntegrationEventConsumerRegistration`1");
        integrationEventInterfaces.ShouldNotContain("SharedKernel.Mediator.INotification");
        handlerInterfaces.ShouldNotContain("SharedKernel.Mediator.INotificationHandler`1");
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
