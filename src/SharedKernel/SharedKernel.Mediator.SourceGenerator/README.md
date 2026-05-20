# SharedKernel.Mediator.SourceGenerator

Incremental source generator for `SharedKernel.Mediator`.

## Purpose

This Roslyn component discovers mediator contracts in the owning
project and emits generated source for the mediator toolchain.

## Current Scope

- Builds the discovery model from request, handler, pipeline,
  notification, stream, and module contracts
- Emits the readable discovery report
- Reports discovery diagnostics for missing handlers, multiple
  handlers, invalid request-handler signatures, inaccessible
  handlers, and duplicate generated registrations
- Emits generated DI registration for handlers, pipelines,
  notifications, and stream handlers
- Emits the generated `AppMediator` shell plus request, stream,
  and notification dispatch helpers into the consumer
  compilation, including per-notification sequential or
  parallel fan-out
- Can optionally emit a generated call-graph JSON artifact wrapper
  when `sharedkernel_mediator_emit_call_graph_json = true`
- Packs as an analyzer-style assembly under `analyzers/dotnet/cs`

## AOT and Trimming

The repository does not keep a dedicated mediator AOT sample project; the supported posture is to
document compatibility and keep the runtime and abstractions packages marked `IsAotCompatible=true`.

## Dependencies

- `Microsoft.CodeAnalysis.Analyzers`
- `Microsoft.CodeAnalysis.CSharp`

## See Also

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator.GeneratorTests](../../../tests/SharedKernel.Mediator.GeneratorTests/README.md)
