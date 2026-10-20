# ADR-023: Separate domain and integration-event dispatch

Initial decision: 2026-06-21. Status: superseded — 2026-07-19 by [generated messaging dispatch ownership](20260719-generated-messaging-dispatch-ownership.md).

## Context and original decision

Domain events describe facts within an aggregate boundary; integration events are explicit versioned contracts crossing process or module boundaries. Their public APIs must remain distinct and compiler-safe. A domain event must not automatically publish an integration event.

The original design used separate dispatcher/handler adapters over mediator capabilities. This reused generated dispatch rather than introducing a disconnected event bus, but adapter registration and runtime lookup duplicated ownership already known at compile time.

## Superseding decision

Preserve the conceptual event distinction while using direct generated domain-to-outbox mappings and closed typed integration-event delivery. Domain dispatch belongs in `PandaBear.Domain`; transport-neutral integration contracts belong in `PandaBear.Messaging.IntegrationEvents`. Durable outbox/inbox mechanics and external transport composition remain explicit provider responsibilities.

Do not restore superseded runtime adapters merely to retain the historical implementation. Reusing a generic notification contract for every event was rejected because it hides lifecycle differences; automatic conversion was rejected because internal facts do not define external contracts.

[Decision index](../README.md).
