# ADR-002: Result Pattern Over Exceptions

**Status**: Accepted — 2025-11-08

## Context

Validation failures and business rule violations were throwing exceptions, making error paths implicit
and hard to test. Exceptions for expected domain failures introduce performance overhead and obscure
control flow.

## Decision

Use the **Result pattern** for all domain operations that can fail:

- `Result` for operations with no return value.
- `Result<T>` for operations that return a value on success.
- `ResultStatus` enum categorizes failures (Failure, Invalid, NotFound, Conflict, etc.).
- `ResultError` record carries error details (status, message, context).
- Reserve exceptions for truly exceptional, unrecoverable failures (e.g., infrastructure errors).

## Consequences

### Pros

- Explicit error handling at compile time — failure paths are visible in method signatures.
- No performance overhead of exceptions for expected validation/business failures.
- Railway-oriented programming enables operation chaining (`Map`, `Bind`).
- Easy to test both success and failure paths with deterministic outcomes.
- Clear separation between domain errors and infrastructure exceptions.

### Cons

- Callers must check `IsSuccess` before accessing `Value`.
- More verbose than exceptions for truly exceptional cases.
- Requires team discipline to use consistently.

## Alternatives considered

- Throwing exceptions for validation failures — rejected due to performance and implicit control flow.
- Using nullable types for failure — rejected as it doesn't carry error information.

## Links

- [Back to ADR Index](../README.md)
- Related: [ADR-001: Domain Validation with Factory Methods](https://github.com/danigutsch/ViajantesTurismo/blob/9116ad887459172c4ad15a65259ffe587bcbb902/docs/adr/20251108-domain-validation-factory-methods.md)

## Standalone ownership update (2026-10-20)

Copied from ViajantesTurismo commit `9116ad887459172c4ad15a65259ffe587bcbb902`. Historical application examples and API names preserve the decision rationale. Reusable result, mediator, generator, and event-dispatch behavior now belongs here; application business policy and composition remain consumer-owned. [PandaBear namespaces](20261020-pandabear-namespaces.md) supersede historical namespace examples. Current module APIs and tests take precedence over historical API shapes.
