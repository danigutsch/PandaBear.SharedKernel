# ADR-024: CloudEvents as an integration-event adapter

Status: accepted — 2026-06-21. Reviewed for the standalone library: 2026-10-20.

## Context

Cross-process events need stable identity, source, type, subject, timestamp, content type, and schema metadata. Interoperability should not force transport SDK types into every event contract or domain model.

## Decision

Keep typed contracts and dispatch boundaries in `PandaBear.Messaging.IntegrationEvents`. Keep the CloudEvents SDK and explicit transport mapping in `PandaBear.Messaging.IntegrationEvents.CloudEvents`. Consumers own concrete payload contracts. Domain events never become CloudEvents implicitly.

A neutral envelope adapter, if required by a concrete use case, belongs behind its own capability boundary rather than changing the typed integration-event contract. The current typed adapter does not establish a requirement to add such a package.

## Consequences and alternatives

Contracts remain typed and dependency-light; infrastructure performs interoperability mapping explicitly. Putting the CloudEvents SDK in the neutral contracts package was rejected because it leaks envelope concerns into every caller. A custom replacement envelope was rejected where the standard already supplies required interoperability metadata. Using transport envelopes for domain events was rejected because local domain facts and cross-process messages have different lifecycles.

[Decision index](../README.md); [messaging terminology](20260705-messaging-event-abstractions.md); [CloudEvents specification](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md).
