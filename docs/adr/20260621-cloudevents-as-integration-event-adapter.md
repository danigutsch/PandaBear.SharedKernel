# ADR-024: CloudEvents as Integration Event Adapter

**Status**: Accepted - 2026-06-21

## Context

Integration events need stable metadata for event identity, source, type, subject, timestamp,
content type, and future interoperability. Dapr uses CloudEvents for pub/sub messages, and
CloudEvents is a standard envelope for event-driven integration.

At the same time, bounded-context domain and application projects should not depend on transport
envelope SDK types just to define typed integration event contracts.

## Decision

Use CloudEvents as an adapter for integration-event transport metadata, not as the core integration
event abstraction.

- `SharedKernel.Messaging.IntegrationEvents` remains dependency-free and owns typed integration-event
  abstractions, envelope conventions, and dispatch boundaries. Each application/bounded context owns
  its concrete payload contract; ADR-040 therefore keeps `BrandingSettingsChangedIntegrationEvent` in a
  ViajantesTurismo-owned Branding integration-contract module.
- `SharedKernel.Messaging.IntegrationEvents.CloudEvents` references the CloudEvents SDK and maps typed
  integration events to and from CloudEvents.
- Domain events never use CloudEvents.

Use `SharedKernel.Messaging.CloudEvents` only for a future neutral envelope adapter.

## Consequences

- Integration event contracts stay typed and lightweight.
- CloudEvents interoperability is available at transport boundaries.
- Future Dapr-compatible pub/sub behavior can be added without changing domain/application code.
- Infrastructure or adapter layers must perform CloudEvents mapping explicitly.
- CloudEvents SDK dependencies are isolated to the adapter project.

## Alternatives Considered

1. **Reference CloudEvents SDK from the core integration event project**
   Rejected because it would leak transport envelope details into every bounded context that defines
   or handles integration events.

2. **Define a custom envelope instead of CloudEvents**
   Rejected because CloudEvents already covers the interoperability metadata needed for future
   transport adapters.

3. **Use CloudEvents for domain events too**
   Rejected because domain events are domain-model facts, not transport messages.

## Links

- [Back to ADR Index](../README.md)
- Related: [Events and Messaging](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/domain/EVENTS_AND_MESSAGING.md)
- Related: [ADR-040: Branding Module Co-hosting and State Distribution](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/adr/20260724-branding-module-co-hosting-and-state-distribution.md)

## Standalone ownership update (2026-10-20)

Copied from ViajantesTurismo commit `9116ad887459172c4ad15a65259ffe587bcbb902`. Historical application examples and API names preserve the decision rationale. Reusable result, mediator, generator, and event-dispatch behavior now belongs here; application business policy and composition remain consumer-owned. [PandaBear namespaces](20261020-pandabear-namespaces.md) supersede historical namespace examples. Current module APIs and tests take precedence over historical API shapes.
