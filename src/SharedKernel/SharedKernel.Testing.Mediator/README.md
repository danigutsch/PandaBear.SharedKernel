# SharedKernel.Testing.Mediator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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

- [SharedKernel.Mediator.Abstractions](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Mediator.Abstractions/README.md)

## See Also

- `tests/SharedKernel.Mediator.Tests`
- `tests/SharedKernel.Mediator.GeneratorTests`

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Mediator`. C# namespace: `PandaBear.Testing.Mediator`. Assembly/project identity: `SharedKernel.Testing.Mediator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
