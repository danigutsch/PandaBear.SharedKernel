# SharedKernel.Mediator

Runtime support surface for `SharedKernel.Mediator`.

## Purpose

This project is the runtime-side companion to the abstractions package. It is the place for
concrete mediator services and lightweight runtime helpers that should remain available even when
source generation owns the main dispatch path.

## Current State

The consumer-side source generator now emits the `AppMediator` shell used by generated DI
registration.
This runtime package stays focused on shared abstractions-facing runtime support that can remain
available while source generation owns mediator composition and request dispatch.

The package now also exposes an optional activity-based observability behavior:

- `SharedKernelMediatorActivitySource` defines the stable `ActivitySource` name
  `SharedKernel.Mediator`.
- `ActivityBehavior<TRequest, TResponse>` adds an internal request activity around mediator pipeline
  execution.

## AOT and Trimming

This package is marked `IsAotCompatible=true` so the SDK enables trim and Native AOT analysis for
the public runtime surface.
The repository does not keep a dedicated mediator AOT benchmark project; instead,
`SharedKernel.Mediator.PackageConsumptionTests` publishes a fresh generated consumer with
`PublishAot=true` and records publish success, trim warnings, native binary size, cold start, first
dispatch, and steady-state dispatch metrics.

## Dependencies

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)
- `Microsoft.Extensions.DependencyInjection.Abstractions`

## See Also

- [SharedKernel.Mediator.SourceGenerator](../SharedKernel.Mediator.SourceGenerator/README.md)
- [Mediator.Sample](../../../samples/Mediator/Mediator.Sample/README.md)
