# ADR-026: Domain materialization and persistence boundaries

Status: accepted — 2026-06-21. Reviewed for the standalone library: 2026-10-20.

## Context

Domain factories enforce invariants while EF Core materializes persisted state. Scalar constructor binding and owned-navigation graphs have different requirements; hiding these differences in domain primitives or generated suppressions obscures the actual contract.

## Decision

Use constructor binding for scalar value objects/entities when parameters map to configured properties. Keep private parameterless constructors only where materialization must populate navigations, owned values, or collections after construction. Preserve validated public creation paths and verify persistence behavior with relevant tests.

Keep domain contracts independent of EF Core. Infrastructure adapters own model configuration, backing fields, value converters, and materialization conventions. Reusable EF mechanics belong in focused packages such as `PandaBear.SharedKernel.Domain.EntityFrameworkCore` and `PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore`; consumers own contexts, schemas/tables, migrations, transactions, and operational policy.

Do not add source generation solely to conceal `CS8618`, or blanket `null!` initializers that hide the materialization contract. Any unavoidable suppression must be narrow and reviewed under the [warning policy](../ci/warning-policy.md).

## Consequences and alternatives

Persistence mapping stays explicit without leaking EF APIs into domain primitives. Scalar-owned types avoid unnecessary constructor exceptions; complex owned graphs can retain scoped limitations until the provider supports their shape. A persistence helper inside the domain package and suppression-generating machinery were rejected because they move implementation concerns across the boundary without solving materialization itself.

[Decision index](../README.md); [domain/building-block split](20260621-split-sharedkernel-domain-and-building-blocks.md).
