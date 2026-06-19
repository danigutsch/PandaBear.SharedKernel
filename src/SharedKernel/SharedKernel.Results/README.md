# SharedKernel.Results

Core result primitives for shared-kernel workflows.

## Purpose

This project is the package-style home for shared result-oriented primitives that should not remain
under an app-specific project boundary.

Issue `#75` establishes the core project only.

Follow-up issues in the same epic cover:

- moving the existing `Result` and `Option` primitives into this package
- improving the structured error model
- adding composition APIs
- updating repository consumers
- adding a sample and benchmarks

## Current state

This package now owns the repository's shared result primitives.

It currently provides:

- `Result`
- `Result<T>`
- `Option<T>`
- `ResultStatus`
- `ResultError`
- `ResultErrorCodes`
- `ValidationErrors`
- `ResultExtensions`
- task/value-task composition helpers for result and option flows

Repository consumers have been updated to use `SharedKernel.Results` as the canonical result
package.

## Package conventions

- `PackageId`: `SharedKernel.Results`
- `RootNamespace`: `SharedKernel.Results`
- `IsAotCompatible=true`
- package README included during packing

## Planned scope

The intended long-term scope for this package is:

- `Result`
- `Result<T>`
- `Option<T>`
- shared result error/status types
- small composition helpers appropriate to the repository's current patterns

## Related issues

- `#74` Epic: introduce SharedKernel.Results
- `#75` Create SharedKernel.Results core project
- `#76` Move Result and Option primitives to SharedKernel.Results
- `#77` Improve SharedKernel.Results error model for structured failures
- `#78` Add composition API to SharedKernel.Results
- `#79` Update consuming projects and architecture tests for SharedKernel.Results
- `#80` Add SharedKernel.Results sample project
- `#81` Add SharedKernel.Results benchmarks
