# SharedKernel.Messaging.IntegrationEvents

Typed integration event dispatch abstractions for events crossing bounded-context boundaries.

The core contracts do not reference CloudEvents or transport SDK types.

## Usage

Define typed `IIntegrationEvent` contracts and `IIntegrationEventHandler<TIntegrationEvent>` implementations. Register contracts/consumers with explicit event type names and `JsonTypeInfo<T>` using the service collection extensions. Enqueue through the outbox interfaces; provider persistence and host composition remain separate packages.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents`. C# namespace: `PandaBear.Messaging.IntegrationEvents`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
