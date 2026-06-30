# SharedKernel.Results

Core result primitives for shared-kernel workflows.

## Purpose

This project is the package-style home for shared result-oriented primitives that should not remain
under an app-specific project boundary.

The first slice established the core project only.

Follow-up slices cover:

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

## Related work

- introduce SharedKernel.Results
- create the SharedKernel.Results core project
- move Result and Option primitives to SharedKernel.Results
- improve the SharedKernel.Results error model for structured failures
- add composition APIs to SharedKernel.Results
- update consuming projects and architecture tests for SharedKernel.Results
- add SharedKernel.Results samples and benchmarks
