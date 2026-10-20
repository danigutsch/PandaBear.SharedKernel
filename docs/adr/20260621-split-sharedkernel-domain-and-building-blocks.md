# ADR-022: Split SharedKernel Domain and Building Blocks

**Status**: Accepted - 2026-06-21

## Context

Reusable domain primitives such as `Entity<TId>`, `ValueObject`, and `DateRange` need focused owners.
New Catalog, event sourcing, and event dispatch work needs these primitives without a broad catch-all
dependency.

The solution already uses focused SharedKernel projects for mediator, results, functional helpers,
OpenAPI, and observability. Reusable DDD primitives should follow the same focused package model.

## Decision

Create focused SharedKernel projects for DDD primitives and reusable value objects.

- `SharedKernel.Domain` owns aggregate-root contracts, `IDomainEvent`, and typed domain-event dispatch.
- `SharedKernel.BuildingBlocks` owns reusable identity interfaces and value objects such as `ValueObject` and `DateRange`.

Move existing primitives gradually into these projects. Decide separately whether `Currency` and
sanitizers belong in `SharedKernel.BuildingBlocks`, another focused SharedKernel project, or an owning
bounded context.

## Consequences

- DDD primitives become available to Admin, Catalog, and future bounded contexts without a catch-all
  common dependency.
- Aggregate root and domain event recording can be standardized across bounded contexts.
- Value objects that are truly reusable get a clear home.
- Existing code must be migrated carefully to avoid large noisy changes.
- Domain models use `IIdentified<TId>`, `IEntity<TId>`, `IAggregateRoot<TId>`, and opt-in generated
  identity support where identity behavior is mechanical.
- Future Vogen-like source generation can be added around `SharedKernel.BuildingBlocks` without
  blocking current Catalog work.

## Alternatives Considered

1. **Keep extending a broad shared project**
   Rejected because it would continue shared-project growth without clear ownership.

2. **Put all primitives in one `SharedKernel.Domain` project**
   Rejected because reusable value objects and DDD aggregate primitives have different consumers and
   future tooling needs.

3. **Duplicate primitives in each bounded context**
   Rejected because aggregate and value-object conventions should remain consistent across the
   solution.

## Links

- [Back to ADR Index](../README.md)
- Related: [Events and Messaging](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/domain/EVENTS_AND_MESSAGING.md)

## Standalone ownership update (2026-10-20)

Copied from ViajantesTurismo commit `9116ad887459172c4ad15a65259ffe587bcbb902`; historical application examples remain provenance. This repository now owns reusable library policy. Application composition, schemas, migrations, and adoption remain consumer-owned. C# namespace examples above describe the historical `SharedKernel.*` API and are superseded by [the PandaBear namespace decision](20261020-pandabear-namespaces.md); project, assembly, and package identity decisions remain separate.
