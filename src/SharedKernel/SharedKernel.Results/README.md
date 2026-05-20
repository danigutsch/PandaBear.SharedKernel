# SharedKernel.Results

Core result and option primitives for shared-kernel workflows.

## Purpose

This project is the shared home for result-oriented primitives that model success, failure, and
 optional values across domain and application code.

It is intended to provide a dependency-light package surface that can stay reusable across bounded
 contexts and can later compose cleanly with other shared-kernel packages such as
 `SharedKernel.Mediator`.

## Current State

This project currently provides:

- `Option<T>` for optional non-null values
- `Result` and `Result<T>` for success and failure flows
- `ResultError` and `ResultStatus` for shared error details and status mapping
- composition helpers such as `Map`, `Bind`, `Match`, `TryGetValue`, `TryGetError`, and `ToResult`

The package is intentionally small and dependency-light.
Future work can extend the composition surface and adoption across the rest of the repository.

## Benchmark Notes

Benchmarks for this package live under `benchmarks/SharedKernel.Results.Benchmarks`.

Current benchmark findings:

- the current `readonly struct` carrier shape is already efficient for normal `Result<T>` payloads
- changing `Result` or `Option` to `ref struct` is not justified by current measurements
- larger struct payloads do amplify copy-sensitive paths such as `TryGetValue`
- benchmark-only `in` experiments help some large-struct projection helpers, but not consistently enough
  to justify broad public API changes

The current recommendation is to keep the public `Result` and `Option` type shapes stable and use
targeted internal experiments only if a specific large-struct hot path proves to matter.

## AOT and Trimming

This package is marked `IsAotCompatible=true` so trim and Native AOT analyzers run against the
 public surface.

## Dependencies

None.

## See Also

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator](../SharedKernel.Mediator/README.md)
