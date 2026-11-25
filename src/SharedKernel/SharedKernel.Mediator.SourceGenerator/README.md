# SharedKernel.Mediator.SourceGenerator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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
  when explicitly enabled for the build
- Packs as an analyzer-style assembly under `analyzers/dotnet/cs`

## Call graph schema

Build with `-p:SharedKernelMediatorEmitCallGraphJson=true -p:EmitCompilerGeneratedFiles=true` to inspect
`SharedKernel.Mediator.Generated.CallGraph.g.cs`. A valid build property overrides the global analyzer
option `sharedkernel_mediator_emit_call_graph_json`. The generated source wraps the JSON in the raw
string literal of `SharedKernel.Mediator.Generated.MediatorCallGraph.Json`. Documentation tools can
read that literal from the generated source without loading the application assembly.

The top-level `schemaVersion` is an integer. Version 1 contains three arrays.

| Array | Content |
| --- | --- |
| `requests` | Request and response names, request kind, handler, ordered pipelines, and generator AOT classification. Request kinds are `request`, `command`, `command-with-response`, and `query`. |
| `notifications` | Notification name, handlers in generated dispatch order, and `sequential` or `parallel` dispatch. |
| `streamRequests` | Stream request and item response names, stream handler, ordered stream pipelines, and generator AOT classification. |

Entries are ordered by the discovery model. Equivalent declaration order changes preserve the
generated document. A `handler` value of `<none>` indicates that discovery found no unique handler.
Consumers should check the schema version before rendering and accept additive fields in that
version. Earlier unversioned output omits `schemaVersion` and `kind`.

## AOT and Trimming

The repository does not keep a dedicated mediator AOT sample project; the supported posture is to
document compatibility and keep the runtime and abstractions packages marked `IsAotCompatible=true`.

## Dependencies

- `Microsoft.CodeAnalysis.Analyzers`
- `Microsoft.CodeAnalysis.CSharp`

## Build-Time Timing Baseline

Use the sample consumer project when profiling generator execution. Building the generator project itself does not
exercise `MediatorGenerator` as an analyzer.

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
- `SharedKernel.Mediator.SourceGenerator.MediatorGenerator`: `0.084` seconds (`93%` of generator time)

Treat this as a local reference point rather than a CI budget. Hardware, warm caches, and concurrent analyzer load
can shift absolute timings, so compare trends more than raw milliseconds.

## See Also

- [SharedKernel.Mediator.Abstractions](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator.GeneratorTests](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tests/SharedKernel.Mediator.GeneratorTests/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Mediator.SourceGenerator`. C# namespace: `SharedKernel.Mediator.SourceGenerator`. Assembly/project identity: `SharedKernel.Mediator.SourceGenerator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
