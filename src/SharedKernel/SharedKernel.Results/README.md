# SharedKernel.Results

Core result and option primitives for shared-kernel workflows.

## Purpose

This project is the shared home for result-oriented primitives that model success, failure, and
 optional values across domain and application code.

It is intended to provide a dependency-light package surface that can stay reusable across bounded
 contexts and can later compose cleanly with other shared-kernel packages such as
 `SharedKernel.Mediator`.

## Current State

This project currently establishes the package and repository structure for the shared-kernel
 results package.
Follow-up work will migrate the existing `Result`, `Result<T>`, `Option<T>`, and related types from
 `ViajantesTurismo.Common` into this project.

## AOT and Trimming

This package is marked `IsAotCompatible=true` so trim and Native AOT analyzers run against the
 public surface.

## Dependencies

None.

## See Also

- [SharedKernel.Mediator.Abstractions](../../Mediator/SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator](../../Mediator/SharedKernel.Mediator/README.md)
