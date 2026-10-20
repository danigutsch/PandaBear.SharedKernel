# ADR-002: Result pattern over exceptions

Status: accepted — 2025-11-08. Reviewed for the standalone library: 2026-10-20.

## Context

Expected validation and business failures need explicit, testable return paths. Exceptions hide these paths in otherwise ordinary operations and make callers infer behavior from implementation details.

## Decision

Use `PandaBear.Results.Result` and `Result<T>` to represent expected success or failure, with `ResultStatus`, structured error details, and validation errors. Reserve exceptions for argument-contract violations and exceptional failures rather than ordinary negative outcomes. Document stable exception conditions using the [exception documentation policy](../EXCEPTION_DOCUMENTATION.md).

Callers inspect success/failure before accessing payloads. Composition helpers keep error propagation explicit across synchronous, task, and value-task flows. HTTP status mapping belongs in the ASP.NET adapter, not the transport-neutral result package. Consumers own their business error catalogs and validation rules.

## Consequences and alternatives

Result types make expected failure visible in signatures and allow deterministic branch tests. Callers must handle outcomes deliberately; Results do not replace exceptions for every failure. Nullable return values were rejected as a general failure contract because they lose structured details. Throwing exceptions for every validation failure was rejected because expected negative outcomes should be ordinary control flow.

[Decision index](../README.md).
