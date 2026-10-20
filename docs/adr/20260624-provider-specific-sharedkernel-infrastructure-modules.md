# ADR-027: Provider-specific infrastructure modules

Status: accepted — 2026-06-24. Reviewed for the standalone library: 2026-10-20.

## Context

Reusable storage and hosting integrations need external dependencies and provider behavior. Putting these dependencies in neutral contracts prevents consumers from selecting integrations independently; keeping all reusable mechanics in hosts duplicates infrastructure.

## Decision

Separate neutral capability contracts from adapters. Package IDs use `PandaBear.SharedKernel.<Capability>` for neutral modules and capability-first provider/adapter suffixes for integrations: `.Npgsql`, `.CloudEvents`, or `.EntityFrameworkCore` when EF itself is the relevant boundary. C# namespaces use `PandaBear.*`.

- Neutral contracts must not depend on EF Core, Npgsql, Dapper, broker/storage SDKs, telemetry exporters, or consumer infrastructure.
- Provider modules may depend on their neutral contracts and the external SDKs needed for the current integration. Keep abstractions inward-facing.
- Host composition owns provider configuration, authentication, retry/timeout policy, schemas, migrations, read models, retention, and operational decisions.
- Hosting integrations remain explicit modules; telemetry registration must not turn neutral contracts into an exporter dependency.
- New adapter packages require an existing/current neutral contract, a real integration use case, and an independent testing boundary. Do not create speculative adapters or umbrella provider dependencies.

## PostgreSQL-specific primitives

`PandaBear.Npgsql` may expose inherently PostgreSQL-specific primitives without manufacturing a neutral abstraction. Transaction and session advisory locks use parameterized PostgreSQL operations on caller-supplied connections/transactions or data sources. The caller retains the transaction or lease through the critical section; disposal must release session ownership.

The module does not own schemas, lock-key derivation, authentication, connection-string configuration, retry policy, or transaction lifetime. Tests must prove same-key contention, distinct-key concurrency, cancellation, and release after commit, rollback, or disposal. Do not weaken those guarantees when factoring provider code.

## Consequences and alternatives

Provider dependencies remain optional and independently testable. Consumers retain data and deployment ownership. Putting provider SDKs in neutral packages was rejected for dependency leakage; duplicating stable provider mechanics in every host was rejected where a real reusable contract already exists. A new Dapper/cloud adapter requires a demonstrated use case rather than a naming placeholder.

[Decision index](../README.md); [materialization boundary](20260621-domain-materialization-and-sharedkernel-persistence-boundaries.md); [CloudEvents adapter](20260621-cloudevents-as-integration-event-adapter.md).
