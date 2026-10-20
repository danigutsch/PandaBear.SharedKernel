# ADR-027: Provider-Specific SharedKernel Infrastructure Modules

## Context

SharedKernel abstractions are intended to be reusable across bounded contexts, but concrete
infrastructure implementations need external packages and provider-specific operational behavior.
Examples include PostgreSQL event stores, Redis idempotency stores, CloudEvents adapters, and future
inbox/outbox persistence.

Without a consistent boundary, provider-specific code can drift into bounded-context infrastructure
projects or into provider-neutral SharedKernel abstraction projects.

## Decision

Provider-neutral abstractions and provider-specific implementations must live in separate modules.

Use this naming pattern:

- `SharedKernel.<Capability>` for provider-neutral contracts and primitives.
- `SharedKernel.<Capability>.<Provider>` for reusable provider implementations.
- `SharedKernel.<Capability>.<Adapter>` for reusable non-provider adapters when the adapter standard is
  the meaningful boundary, such as `CloudEvents`.
- `SharedKernel.<Capability>.EntityFrameworkCore.<Provider>` for reusable EF Core adapters, when the
  EF Core dependency is the boundary.
- `<BoundedContext>.Infrastructure` for bounded-context composition, schema ownership, migrations,
  and read models.

Current examples:

- `SharedKernel.EventSourcing` owns `IEventStore`, `IProjectionCheckpointStore`, stream identifiers,
  expected revisions, and event envelopes.
- `SharedKernel.EventSourcing.Npgsql` owns reusable PostgreSQL event-store and projection
  checkpoint implementations plus provider telemetry names.
- `SharedKernel.Messaging.IntegrationEvents` owns typed integration-event contracts.
- `SharedKernel.Messaging.IntegrationEvents.CloudEvents` owns CloudEvents mapping as an adapter.
- `SharedKernel.Idempotency` owns idempotency contracts and value types.

### Raw Npgsql primitives

`SharedKernel.Npgsql` may contain small, reusable raw-Npgsql primitives without a neutral contract when
the primitive is inherently PostgreSQL-specific. Its scope includes
`PostgreSqlTransactionAdvisoryLock`, which acquires a parameterized
`pg_advisory_xact_lock` on a caller-supplied `NpgsqlConnection`, `NpgsqlTransaction`, and 64-bit lock
key, and `PostgreSqlSessionAdvisoryLock`, which acquires a parameterized `pg_advisory_lock` on a
dedicated connection from a caller-supplied `NpgsqlDataSource` and returns a disposable lease.

The module does not own schemas, migrations, connection-string configuration, lock-key derivation,
retry or timeout policy, distributed-cache behavior, authentication/token behavior, or transaction
lifetime. Callers retain the transaction through the critical section. For session locks, callers
retain the lease through the critical section; disposing it closes the lock-owning connection and
releases the lock.

Current callers are `SharedKernel.EventSourcing.Npgsql.PostgreSqlEventStore` and
`ViajantesTurismo.Management.Web.ProtectedDistributedUserTokenStore`. The session-scoped primitive is
used by `ViajantesTurismo.Catalog.Infrastructure.PostgreSqlCatalogTourSlugLock`. Independent
`SharedKernel.Npgsql.Tests` integration tests must prove same-key contention, distinct-key concurrency,
cancellation, and release after commit, rollback, and disposal.

Adapter modules may reference the external packages needed for their implementation, such as
`Npgsql` for PostgreSQL or CloudEvents SDK packages for CloudEvents mapping. Provider-neutral modules
must not.

## Dependency Rules

Reference direction stays inward:

- Domain projects may reference provider-neutral SharedKernel contracts and primitives only.
- Application projects may reference provider-neutral contracts, but not concrete infrastructure
  adapters.
- Provider-neutral SharedKernel modules must not reference EF Core, `Npgsql`, Dapper, Azure SDKs,
  broker clients, storage clients, telemetry exporters, or bounded-context infrastructure projects.
- SharedKernel adapter modules may reference only their neutral contract module and the external
  package needed to implement the adapter.
- Bounded-context infrastructure projects may reference provider-specific adapters during composition.
- API and host projects may reference infrastructure only at the composition root.
- ServiceDefaults may register stable telemetry names, but must not become the owner of provider
  adapters.

Examples:

- EF Core owned by one bounded context stays in `<BoundedContext>.Infrastructure` because the DbContext,
  migrations, and schema policy are context-specific.
- Reusable EF Core provider code must include `EntityFrameworkCore` in the package name, for example
  `SharedKernel.<Capability>.EntityFrameworkCore.PostgreSQL`.
- Reusable raw PostgreSQL event-store code belongs in `SharedKernel.EventSourcing.Npgsql` because it
  implements storage-neutral event-sourcing contracts without owning a bounded-context schema.
- Optional Dapper implementations should use `SharedKernel.<Capability>.Dapper` only when the query or
  store contract is reusable outside one context.
- Azure Blob, Queue, Service Bus, or Event Hubs clients should use a capability-first name such as
  `SharedKernel.<Capability>.AzureBlobStorage` or `SharedKernel.<Capability>.AzureServiceBus` only after
  a stable neutral contract exists.
- Messaging and storage client SDKs must stay out of domain/application projects; use an adapter or the
  owning infrastructure project.
- Telemetry exporters are startup/runtime adapters. Keep exporter dependencies in host/service-default
  composition unless a reusable observability adapter has at least two real consumers.

## Split Threshold

Create a new adapter package only when all of these are true:

1. A provider-neutral contract already exists or is being added in the same vertical slice.
2. The implementation wraps an external dependency that should not leak into neutral contracts.
3. At least two concrete consumers need the implementation now, or an accepted issue/ADR records the
   near-term second consumer and migration path.
4. The adapter can be named by capability and provider without mentioning a bounded context.
5. Tests can validate the adapter independently from one bounded context.

Do not split when the code is one context's DbContext, migrations, read model, seed workflow, or
composition glue. Keep it local until reuse is real.

Bounded-context infrastructure remains responsible for:

- Choosing provider modules during composition.
- Owning schema names and migrations for context-owned data.
- Owning read-model tables and projection rebuild workflows.
- Enforcing context-specific retention, indexing, and operational policies.

ServiceDefaults may register stable telemetry source and meter names for provider modules, but it
should avoid direct project references to provider modules unless a future ADR explicitly accepts that
dependency.

## Consequences

- Reusable provider code has one home and can be tested independently.
- Provider-neutral contracts stay small and dependency-light.
- Bounded contexts keep ownership of schemas, read models, and operational decisions.
- ServiceDefaults can opt into provider telemetry without coupling application defaults to every
  provider implementation package.
- New provider modules need explicit names and telemetry contracts before becoming shared
  infrastructure.

## Candidate Inventory

| Capability | Neutral Module | Provider Module | Status |
| --- | --- | --- | --- |
| Event sourcing | `SharedKernel.EventSourcing` | `SharedKernel.EventSourcing.Npgsql` | Implemented |
| Integration-event envelope mapping | `SharedKernel.Messaging.IntegrationEvents` | `SharedKernel.Messaging.IntegrationEvents.CloudEvents` | Implemented |
| Idempotency | `SharedKernel.Idempotency` | `SharedKernel.Idempotency.EntityFrameworkCore` | Implemented |
| Inbox/outbox | `SharedKernel.Messaging.IntegrationEvents` | `SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore` | Implemented |
| Caching | Future cache contracts only if reused | Redis-backed cache adapters | Candidate |
| Telemetry export | `SharedKernel.Observability` only for neutral contracts | Exporter-specific adapter only after reuse is proven | Candidate |

Current dependency audit:

| Area | Current dependency | Boundary decision |
| --- | --- | --- |
| Admin persistence | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | Keep in `ViajantesTurismo.Admin.Infrastructure`; DbContexts and migrations are context-owned. |
| Catalog persistence | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | Keep in `ViajantesTurismo.Catalog.Infrastructure`; DbContexts and migrations are context-owned. |
| Event sourcing | `Npgsql` | Keep in `SharedKernel.EventSourcing.Npgsql`; this is a reusable raw Npgsql adapter. |
| Public web output caching | `Aspire.StackExchange.Redis.OutputCaching` | Keep in `ViajantesTurismo.Management.Web`; no shared cache contract exists. |
| Service telemetry | OpenTelemetry exporter/instrumentation packages | Keep in `ViajantesTurismo.ServiceDefaults`; these are startup/runtime composition dependencies. |
| Grafana hosting | Aspire hosting packages | Keep in `SharedKernel.Aspire.Hosting.Grafana`; the hosting integration is explicit. |

Follow-up split and audit work must reference these rules:

- audit current dependency leakage
- cover PostgreSQL raw Npgsql and EF Core adapter boundaries
- cover optional Dapper adapter naming
- cover non-database external dependency adapters
- cover architecture tests for these dependency boundaries
- update docs and package references after accepted splits

## Alternatives

- Put all provider code in bounded-context infrastructure projects. This keeps dependencies local but
  duplicates reusable infrastructure and makes cross-context behavior inconsistent.
- Put provider implementations in neutral SharedKernel projects. This simplifies discovery but leaks
  provider dependencies into contracts and makes future provider replacement harder.
- Add provider references directly to ServiceDefaults. This simplifies source/meter registration but
  makes every application depend on providers it may not use.

## Status

Accepted.

## Links

- [ADR Index](../README.md)
- [ADR-024: CloudEvents as Integration Event Adapter](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/adr/20260621-cloudevents-as-integration-event-adapter.md)
- [ADR-025: Event Source Catalog Tour Presentation](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/adr/20260621-event-source-catalog-tour-presentation.md)
- [ADR-026: Domain Materialization and SharedKernel Persistence Boundaries](20260621-domain-materialization-and-sharedkernel-persistence-boundaries.md)
- [Events and Messaging](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/domain/EVENTS_AND_MESSAGING.md)

## Standalone ownership update (2026-10-20)

Copied from ViajantesTurismo commit `9116ad887459172c4ad15a65259ffe587bcbb902`; historical application examples remain provenance. This repository now owns reusable library policy. Application composition, schemas, migrations, and adoption remain consumer-owned. C# namespace examples above describe the historical `SharedKernel.*` API and are superseded by [the PandaBear namespace decision](20261020-pandabear-namespaces.md); project, assembly, and package identity decisions remain separate.
