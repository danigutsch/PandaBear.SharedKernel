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

## Build-Time Timing Baseline

Use the sample consumer project when profiling generator execution. Building the generator project itself does not
exercise `SharedKernelMediatorGenerator` as an analyzer.

### Baseline command

```bash
dotnet build samples/Mediator/Mediator.Sample/Mediator.Sample.csproj /t:Rebuild /p:ReportAnalyzer=true /bl:mediator-sample.binlog -v:detailed
```

### How to inspect it

1. Open `mediator-sample.binlog` in MSBuild Structured Log Viewer.
2. Find the `Csc` task for `Mediator.Sample`.
3. Record both the `Total analyzer execution time` section and the `Generator` subsection.
4. Compare future runs against the same sample project, target, and verbosity to keep the baseline meaningful.

### Current baseline snapshot

- Command run on 2026-06-14:
  `dotnet build samples/Mediator/Mediator.Sample/Mediator.Sample.csproj /t:Rebuild /p:ReportAnalyzer=true /bl:mediator-sample.binlog -v:detailed`
- Total analyzer execution time: `0.589` seconds
- `SharedKernel.Mediator.SourceGenerator.SharedKernelMediatorGenerator`: `0.084` seconds (`93%` of generator time)

Treat this as a local reference point rather than a CI budget. Hardware, warm caches, and concurrent analyzer load
can shift absolute timings, so compare trends more than raw milliseconds.

## See Also

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator.GeneratorTests](../../../tests/SharedKernel.Mediator.GeneratorTests/README.md)
