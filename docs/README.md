# Documentation

- [Architecture and generated diagrams](architecture/README.md)
- [Development and quality](development.md)
- [Exception documentation](EXCEPTION_DOCUMENTATION.md)
- [Warning policy](ci/warning-policy.md)
- [Action and image pin review](ci/pin-update-review.md)
- [Release policy and consumer adoption](release-policy.md)
- [Domain and building-block split](adr/20260621-split-sharedkernel-domain-and-building-blocks.md)
- [Domain materialization and persistence boundaries](adr/20260621-domain-materialization-and-sharedkernel-persistence-boundaries.md)
- [Provider-specific modules](adr/20260624-provider-specific-sharedkernel-infrastructure-modules.md)
- [Breaking namespace decision](adr/20261020-pandabear-namespaces.md)

Module README files beside source own API usage and examples. Samples and benchmarks remain under `samples/` and `benchmarks/`. Historical extraction records remain under `provenance/`. Application runtime and deployment diagrams stay in ViajantesTurismo.

## Additional reusable decisions

- [20251108-result-pattern-over-exceptions](adr/20251108-result-pattern-over-exceptions.md)
- [20260621-cloudevents-as-integration-event-adapter](adr/20260621-cloudevents-as-integration-event-adapter.md)
- [20260621-separate-domain-and-integration-event-dispatch](adr/20260621-separate-domain-and-integration-event-dispatch.md)
- [20260705-messaging-event-abstractions](adr/20260705-messaging-event-abstractions.md)
- [20260719-generated-messaging-dispatch-ownership](adr/20260719-generated-messaging-dispatch-ownership.md)

Other donor decisions were reviewed by ownership: booking, pricing, Catalog projections, API routes, BFF security, database schemas, and application CI remain consumer-owned. Library versioning supersedes the application release-governance workflow through [release policy](release-policy.md). Mediator and generator mechanics are covered by generated messaging dispatch ownership; module README files and tests own their current API contracts.

- [API contract versioning](API_VERSIONING.md)
