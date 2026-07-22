# SharedKernel.Domain

Persistence-agnostic DDD primitives shared by bounded contexts.

The package also owns typed in-process domain-event dispatch contracts. Generated modules contribute
closed `IDomainEventDispatchHandler` implementations, and the scoped `CompositeDomainEventDispatcher`
invokes applicable handlers without a runtime registry or mediator adapter. The dispatch APIs retain
the `SharedKernel.Domain` namespace and package boundary.
