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

## AOT and Trimming

This package is marked `IsAotCompatible=true` so trim and Native AOT analyzers run against the
 public surface.

## Dependencies

None.

## See Also

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator](../SharedKernel.Mediator/README.md)
