# ADR-033: Messaging Event Abstractions and CloudEvents Boundary

**Status**: Accepted - 2026-07-19

## Context

The repository separates domain events from integration events, and treats CloudEvents as a transport
adapter. The `PandaBear.Messaging.IntegrationEvents.CloudEvents` name keeps that adapter scoped to
typed integration events; `PandaBear.Messaging.CloudEvents` stays reserved for a future neutral
envelope adapter.

The EF Core outbox persists durable message records that contain serialized event metadata, payload,
and runtime state. Future work may add other event-like messages, inbound records, publishers, and
transport adapters. The language must separate facts, envelopes, messages, and persistence rows.

## Decision

Use messaging language for transport/runtime abstractions and event language for facts/contracts.

- Keep typed event contracts in explicit conceptual modules:
    - `PandaBear.Domain` for domain event contracts, recording, and in-process dispatch.
    - `PandaBear.Messaging.IntegrationEvents` for typed cross-boundary event contracts and handlers.
    - `PandaBear.EventSourcing` for event-stream persistence and projections.
- Introduce `PandaBear.Messaging` as the storage-neutral home for message and envelope concepts
  that are not specific to one event contract family.
- Move the typed integration-event CloudEvents adapter naming toward
  `PandaBear.Messaging.IntegrationEvents.CloudEvents`.
- Reserve `PandaBear.Messaging.CloudEvents` for a future neutral `EventEnvelope` to `CloudEvent`
  adapter if one is needed.
- Keep provider-specific persistence in provider modules such as
  `PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore` and
  `PandaBear.Idempotency.EntityFrameworkCore`. EF entities are storage records, not the public event
  contracts.
- Model common event metadata and payload shape once as an `EventEnvelope` concept, then compose it into
  durable records such as outbox and inbox messages.

## Ubiquitous language

| Term | Meaning | Owns persistence? |
| --- | --- | --- |
| `DomainEvent` | Business fact raised by an aggregate inside one bounded context. | No |
| `IntegrationEvent` | Versioned cross-boundary event contract. | No |
| `EventEnvelope` | Serialized event identity, type, time, source, content type, payload, and metadata. | No |
| `Message` | Delivery or processing unit that carries an envelope through a runtime boundary. | No |
| `OutboxMessage` | Durable outbound message with envelope plus publish state. | Yes |
| `InboxMessage` | Durable inbound message with envelope plus processing state or de-duplication state. | Yes |
| `IdempotencyEntry` | Generic operation ledger keyed by scope and key. Can back inbox behavior. | Yes |
| `CloudEvent` | Standards-based event envelope at an interoperability boundary. | No |

Use `Event` for facts and contracts. Use `Message` for delivery records. Use `Envelope` for metadata
and serialized data. Avoid using one class as all three.

## Namespace and package conventions

Use capability-oriented namespaces:

- `PandaBear.Messaging` for neutral message/envelope abstractions.
- `PandaBear.Messaging.IntegrationEvents` for typed integration event contracts and dispatch.
- `PandaBear.Messaging.IntegrationEvents.CloudEvents` for typed integration-event CloudEvents mapping.
- `PandaBear.Messaging.CloudEvents` only for neutral envelope CloudEvents mapping.
- `PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore` for EF-specific outbox/inbox
  persistence.
- `PandaBear.Idempotency.EntityFrameworkCore` for EF-specific idempotency persistence.

Avoid `PandaBear.Events` for messaging runtime abstractions because `Events` is ambiguous in this
solution: it can mean domain events, integration events, event-sourced stream events, or CloudEvents.
Do not keep integration-event contracts at a root integration-events namespace now that the messaging
parent module exists.

## Composition, inheritance, and source generation

Prefer composition for durable message records.

```text
OutboxMessage = EventEnvelope + outbound state
InboxMessage  = EventEnvelope + inbound processing state
```

Composition matches the domain language: an outbox message has an envelope; it is not only an envelope.
It also avoids EF inheritance mapping decisions such as table-per-hierarchy, table-per-type, or
table-per-concrete-type before there is a measured need.

Do not use inheritance as the first step for shared envelope fields. A base class can remove duplicate
properties, but it couples unrelated records to one CLR hierarchy and can leak EF mapping choices into
the model. If a base type is introduced later, it should be an internal implementation detail and should
not replace the envelope concept.

Do not use source generation for the persistence row shape yet. Source generators are useful when they
remove repeated compile-time plumbing, avoid reflection, or support Native AOT. They should not replace
simple composition while there are only a few concrete message records. Keep generators focused on
mechanical mapping, diagnostics, and serializer registration, not business validation or persistence
state machines.

Adopted generator responsibilities:

- Direct exhaustive domain-event-to-outbox mapping.
- AOT-safe typed serialization and envelope delivery for known event contracts.
- Compile-time diagnostics for missing or duplicate consumer handlers and duplicate consumer event
  types.

Non-candidates for now:

- Outbox and inbox state transitions.
- EF Core inheritance or table mapping.
- Business validation rules.

## Consequences

- Integration-event contracts stay strongly typed and dependency-light.
- CloudEvents becomes an adapter for message envelopes, not a dependency of bounded-context code.
- Outbox and inbox records can share envelope validation without sharing persistence state.
- EF provider code remains internal and replaceable.
- The resulting module boundaries are:
  1. Neutral `PandaBear.Messaging` envelope abstractions.
  2. Typed integration-event contracts under `PandaBear.Messaging.IntegrationEvents`.
  3. Focused EF provider packages.
  4. Message/entity language for EF records.
  5. Shared envelopes composed into outbox/inbox records.

## Research notes

- CloudEvents defines an event envelope with attributes such as `id`, `source`, `type`, `time`,
  `datacontenttype`, `data`, and extension attributes.
- Microsoft DDD guidance separates domain events from integration events.
- Transactional outbox stores local state and outbound messages in one transaction; message relays may
  duplicate delivery, so consumers need idempotency.
- Idempotent consumer patterns commonly store processed message identity per subscriber or consumer.
- EF Core inheritance mapping has real schema and performance consequences. Owned or composed values fit
  a shared envelope better than an early hierarchy.
- Roslyn source generators are additive compile-time tools. They are best for repetitive generated code,
  reflection avoidance, and AOT support, not as the first abstraction for simple shared state.

## External references

- [CloudEvents specification](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md)
- [CloudEvents C# SDK](https://github.com/cloudevents/sdk-csharp)
- [.NET namespace design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces)
- [Microsoft domain events guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Microsoft integration events guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/integration-event-based-microservice-communications)
- [Transactional Outbox pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Idempotent Consumer pattern](https://microservices.io/patterns/communication-style/idempotent-consumer.html)
- [MassTransit transactional outbox](https://masstransit.io/documentation/patterns/transactional-outbox)
- [EF Core inheritance](https://learn.microsoft.com/en-us/ef/core/modeling/inheritance)
- [EF Core owned entity types](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
- [Roslyn source generators](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)

## Links

- [Back to ADR Index](../README.md)
- Related: [ADR-024: CloudEvents as Integration Event Adapter](20260621-cloudevents-as-integration-event-adapter.md)


Reviewed for the standalone library: 2026-10-20. Project and assembly identities retain `SharedKernel.*`; package IDs use `PandaBear.SharedKernel.*`.
