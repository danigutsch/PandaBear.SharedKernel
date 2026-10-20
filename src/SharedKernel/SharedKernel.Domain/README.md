# SharedKernel.Domain

Persistence-agnostic DDD primitives shared by bounded contexts.

The package also owns typed in-process domain-event dispatch contracts. Generated modules contribute
closed `IDomainEventDispatchHandler` implementations, and the scoped `CompositeDomainEventDispatcher`
invokes applicable handlers without a runtime registry or mediator adapter. The dispatch APIs retain
the `PandaBear.Domain` namespace and `PandaBear.SharedKernel.Domain` package boundary.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain`. C# namespace: `PandaBear.Domain`. Assembly/project identity: `SharedKernel.Domain`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
