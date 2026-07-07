# SharedKernel.Testing.Mediator

Reference dispatcher test support for `SharedKernel.Mediator`.

## Purpose

This project contains the simple reference implementation used to validate generated behavior
against a known-correct dispatcher in tests.

## Scope

- Explicit registration of request handlers, pipelines, notification handlers, and stream handlers
- Deterministic pipeline ordering based on `PipelineOrderAttribute`
- Exact-type notification publishing for correctness comparisons
- Test-only request, notification, and stream dispatch behavior

## Current State

The project now exposes `ReferenceDispatcherBuilder` and `ReferenceMediator` for test usage.

## Dependencies

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)

## See Also

- `tests/SharedKernel.Mediator.Tests`
- `tests/SharedKernel.Mediator.GeneratorTests`
