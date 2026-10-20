# ADR-023: Separate Domain and Integration Event Dispatch

**Status**: Superseded - 2026-07-19

**Superseded by**: [ADR-039: Generated Messaging Dispatch Ownership](20260719-generated-messaging-dispatch-ownership.md)

ADR-039 preserves the conceptual separation between domain and integration events while replacing the
dispatcher and handler adapters described here with direct generated domain-event-to-outbox mapping and
closed typed integration-event delivery.

## Context

Domain events and integration events have different roles. Domain events describe facts inside one
bounded context and belong to aggregate/application logic. Integration events are explicit contracts
for crossing bounded-context or process boundaries.

The repository already has `SharedKernel.Mediator` with generated dispatch, notification handlers,
module discovery, ordering, and dispatch strategy support. New event dispatch should extend this
model instead of introducing a disconnected event bus.

## Decision

Create separate typed dispatch modules for domain events and integration events.

- A dedicated domain-event module owns `IDomainEventDispatcher` and
  `IDomainEventHandler<TDomainEvent>`.
- `SharedKernel.Messaging.IntegrationEvents` owns `IIntegrationEventDispatcher` and
  `IIntegrationEventHandler<TIntegrationEvent>`.

Both modules may adapt to `SharedKernel.Mediator` internally, but their public abstractions stay
separate and compiler-safe. Domain events do not automatically publish integration events.

## Consequences

- Code cannot accidentally handle an integration event through the domain event API or vice versa.
- Domain event handlers remain local to bounded contexts.
- Integration event publishing remains an explicit application/infrastructure decision.
- Existing mediator source generation can remain the shared dispatch mechanism.
- Additional adapters are needed for durable outbox/inbox and external transports.

## Alternatives Considered

1. **Use `INotification` directly for all events**
   Rejected because it hides the lifecycle difference between domain and integration events.

2. **Create a separate event bus unrelated to `SharedKernel.Mediator`**
   Rejected because it duplicates existing generated dispatch capabilities.

3. **Automatically convert domain events to integration events**
   Rejected because it couples internal aggregate facts to cross-context contracts.

## Links

- [Back to ADR Index](../README.md)
- Related: [Events and Messaging](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/domain/EVENTS_AND_MESSAGING.md)

## Standalone ownership update (2026-10-20)

Copied from ViajantesTurismo commit `9116ad887459172c4ad15a65259ffe587bcbb902`. Historical application examples and API names preserve the decision rationale. Reusable result, mediator, generator, and event-dispatch behavior now belongs here; application business policy and composition remain consumer-owned. [PandaBear namespaces](20261020-pandabear-namespaces.md) supersede historical namespace examples. Current module APIs and tests take precedence over historical API shapes.
