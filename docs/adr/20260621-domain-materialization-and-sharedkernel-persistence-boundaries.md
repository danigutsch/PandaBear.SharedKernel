# ADR-026: Domain Materialization and SharedKernel Persistence Boundaries

**Status**: Accepted — 2026-06-21

## Context

Analyzer hardening exposed scoped `CS8618` exceptions in domain types that keep public creation
behind factories while EF Core uses private parameterless constructors for materialization. Those
constructors are acceptable only when EF cannot bind all required state through a constructor, such
as aggregate roots with owned navigations or collection backing fields.

The same review raised whether SharedKernel should hide materialization concerns in domain
primitives or source generation. That would make persistence concerns part of the Domain layer and
would obscure which invariants are enforced by domain factories versus EF configuration.

## Decision

Use EF Core constructor binding for scalar-only value objects and entities when the constructor
parameters map directly to configured scalar properties. Keep private parameterless constructors
only for aggregate roots or owned graphs where EF still needs to set navigations, owned types, or
collections after construction.

Keep the Domain layer persistence-agnostic:

- Domain types may depend on domain primitives, value objects, result types, and invariant helpers.
- Domain types must not depend on EF Core or persistence-specific helper packages.
- Infrastructure owns EF configuration, backing-field mappings, value converters, and
  materialization conventions.
- Shared EF helpers belong in a focused Infrastructure-side provider package, not
  `SharedKernel.Domain`. For integration messaging this is
  `SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore`; it may supply reusable
  context-qualified mapping mechanics, while the consuming module owns schema/table selection,
  migrations, transactions, and operational policy.
- Do not add source generation solely to hide `CS8618` warnings for existing domain entities.

The representative migration removes EF-only parameterless constructors and their `CS8618`
exceptions from `PersonalInfo` in the Customer aggregate and `Discount` in the Tour/Booking
aggregate. Both are scalar-owned value objects that EF can materialize through constructor binding
while domain callers continue to use validated factory methods.

## Consequences

### Pros

- Reduces analyzer exceptions without adding null-forgiving initializers or persistence APIs to
  domain types.
- Keeps EF mapping decisions explicit in Infrastructure.
- Provides a repeatable pattern for scalar-owned value objects before touching more complex
  aggregate roots.
- Preserves domain factories and invariant validation as the public creation path.

### Cons

- Aggregate roots with owned navigations still need scoped `CS8618` exceptions until they are
  redesigned or EF can bind the full graph safely.
- Developers must understand which constructor shape EF can bind and verify changes with build and
  persistence tests.

## Alternatives Considered

- Use `null!` initializers for all EF materialized members. Rejected because it hides the real
  materialization contract and weakens analyzer signal.
- Put EF materialization helpers in `SharedKernel.Domain`. Rejected because it leaks persistence
  concerns into domain primitives.
- Generate constructors or suppressions with a source generator. Rejected because the current need
  is policy and mapping clarity, not code generation.

## Links

- [Back to ADR Index](../README.md)
- Related: [ADR-001: Domain Validation with Factory Methods](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/adr/20251108-domain-validation-factory-methods.md)
- Related: [ADR-022: Split SharedKernel Domain and Building Blocks](20260621-split-sharedkernel-domain-and-building-blocks.md)
- Related: [ADR-040: Branding Module Co-hosting and State Distribution](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/adr/20260724-branding-module-co-hosting-and-state-distribution.md)

## Standalone ownership update (2026-10-20)

Copied from ViajantesTurismo commit `9116ad887459172c4ad15a65259ffe587bcbb902`; historical application examples remain provenance. This repository now owns reusable library policy. Application composition, schemas, migrations, and adoption remain consumer-owned. C# namespace examples above describe the historical `SharedKernel.*` API and are superseded by [the PandaBear namespace decision](20261020-pandabear-namespaces.md); project, assembly, and package identity decisions remain separate.
