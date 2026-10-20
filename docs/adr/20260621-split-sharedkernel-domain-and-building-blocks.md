# ADR-022: Separate domain primitives and building blocks

Status: accepted — 2026-06-21. Reviewed for the standalone library: 2026-10-20.

## Context

Aggregate contracts, domain events, reusable identity interfaces, and value objects need focused owners. A catch-all shared dependency obscures boundaries and forces unrelated consumers to take dependencies they do not need.

## Decision

`PandaBear.Domain` owns aggregate-root contracts, `IDomainEvent`, and typed in-process domain-event dispatch. `PandaBear.BuildingBlocks` owns reusable identity interfaces and value objects. Keep mechanical identity support opt-in; generators do not own business validation.

Keep package capabilities focused and provider-independent. Add a reusable primitive only when its boundary and current use are clear; a value object specific to one business belongs with its owner. Source namespaces use `PandaBear.*`; assembly/project identities retain `SharedKernel.*`, and package IDs use `PandaBear.SharedKernel.*` as recorded in the [namespace decision](20261020-pandabear-namespaces.md).

## Consequences and alternatives

Consumers can use aggregate contracts without acquiring every utility or persistence adapter. Identity and event conventions remain consistent while unrelated capabilities evolve independently. A broad shared project was rejected for unclear ownership; one combined domain/value-object package was rejected because these capabilities have different consumers. Duplicating mechanical primitives was rejected when a stable reusable contract already exists.

[Decision index](../README.md).
