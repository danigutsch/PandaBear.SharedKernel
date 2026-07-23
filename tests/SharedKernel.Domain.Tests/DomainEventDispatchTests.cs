namespace SharedKernel.Domain.Tests;

public sealed class DomainEventDispatchTests
{
    [Fact]
    public void Domain_event_contracts_do_not_depend_on_mediator()
    {
        // Arrange
        var assembly = typeof(IDomainEventDispatcher).Assembly;
        var referencedAssemblyNames = assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToArray();
        var exportedTypeNames = assembly.GetExportedTypes()
            .Select(static type => type.Name)
            .ToArray();

        // Act
        var dispatcherMethod = typeof(IDomainEventDispatcher).GetMethods()
            .Single(static method => method.Name == nameof(IDomainEventDispatcher.Dispatch) && method.IsGenericMethodDefinition);

        // Assert
        referencedAssemblyNames.ShouldNotContain("SharedKernel.Mediator.Abstractions");
        exportedTypeNames.ShouldNotContain("IDomainEventHandler`1");
        exportedTypeNames.ShouldNotContain("IDomainEventNotificationFactory");
        exportedTypeNames.ShouldNotContain("DomainEventNotification`1");
        exportedTypeNames.ShouldNotContain("DomainEventNotificationHandler`1");
        exportedTypeNames.ShouldNotContain("MediatorDomainEventDispatcher");
        dispatcherMethod.IsGenericMethodDefinition.ShouldBeTrue();
        dispatcherMethod.GetGenericArguments().Single().GetGenericParameterConstraints().ShouldContain(typeof(IDomainEvent));
        typeof(IDomainEventDispatcher).Assembly.ShouldBe(typeof(IDomainEvent).Assembly);
        typeof(IDomainEventDispatcher).Namespace.ShouldBe("SharedKernel.Domain");
    }
}
