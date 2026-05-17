# SharedKernel.Mediator.Benchmarks

Benchmark harness for the `SharedKernel.Mediator` discovery generator and API-shape experiments.

## Scope

- Measure discovery-report generation across the M03 scale points: 10, 100, 1,000, and 5,000 requests
- Compare clean-build, no-op rebuild, and one-handler-edit rebuild scenarios
- Capture generator allocation impact with BenchmarkDotNet's memory diagnoser
- Track generated source length for the baseline and edited inputs inside the benchmark fixture
- Report generated source count and total generated source size for discovery benchmark cases
- Compare direct `ValueTask<T>` and benchmark-only `Task<T>` handler returns
- Compare direct `Unit` command completion alongside the result-returning paths
- Cover class, record class, struct, and readonly record struct requests
- Cover synchronous and asynchronous completion paths
- Add shared benchmark-report columns for median, P95, P99, and benchmark conditions
- Compare generated-style and hand-written mediator DI service-provider build costs
- Measure handler, mediator, and first-dispatch DI costs
- Measure notification publish across sequential and parallel handler-count scale points
- Measure notification exception and cancellation publish paths
- Measure notification publish generation overhead and report generated source size
- Measure stream dispatch across direct, generated, channel-copy, buffered-copy, and manual-iterator
  strategies
- Measure analyzer throughput across clean/diagnostic-heavy inputs and analyzer option toggles
- Measure generated object-switch dispatch separately from typed and generic dispatch
- Measure direct, typed, generic-switch, static-generic-cache, dictionary, frozen-dictionary, and
  object-switch dispatch across request-count scale points
- Report BenchmarkDotNet mean, median, P95, P99, allocation, GC, conditions, and code-size metrics
  for the relevant runtime suites
- Report generated-source size for the relevant generator-focused suites
- Report synthetic generated-source size and benchmark-assembly build time for dispatch-scale cases

## Run

```powershell
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DiscoveryBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *ApiShapeBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DependencyInjectionBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *NotificationPublishBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *NotificationPublishFailureBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *NotificationPublishGenerationBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *PipelineDispatchBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *StreamDispatchFirstItemBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *StreamDispatchEnumerationBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *StreamDispatchCancellationBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *StreamDispatchBackpressureBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *AnalyzerPerformanceBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *ObservabilityDispatchBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *ObjectDispatchBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DispatchScaleBenchmarks*
```

## Notes

- The benchmark sources are synthetic and generated in-memory to isolate discovery-pipeline cost.
- `Clean build` recreates the compilation from source before running the generator.
- `No-op rebuild` reuses the same compilation and generator graph.
- `One-handler edit rebuild` reruns the graph against a compilation whose first handler body changed.
- `DiscoveryBenchmarks` covers the BM011 generator-build matrix across request counts
  `10/100/1,000/5,000`, the three rebuild scenarios, generated source count, and generated source
  size.
- `ApiShapeBenchmarks` isolates handler return-path shape only; the `Task<T>` path exists for
  benchmark comparison and is not part of the production mediator contract.
- The same suite now includes a direct `Unit` command benchmark so BM001 covers the non-result
  mediator command shape as well.
- `DependencyInjectionBenchmarks` uses a generated-style benchmark mediator shell so DI build,
  resolution, and first-dispatch costs stay close to the current generated shape.
- The DI benchmark now covers transient, scoped, and singleton-stateless handler lifetime candidates
  plus handler dependency counts `0`, `1`, and `5`.
- `NotificationPublishBenchmarks` already covers the BM006 sequential handler-count matrix
  `0/1/3/10/50` and also includes the parallel publish path that the checklist still labeled later.
- `NotificationPublishFailureBenchmarks` covers the BM006 exception and cancellation paths for both
  sequential and parallel publish strategies.
- The BM007 stream suites already cover direct stream handler, generated direct return, generated
  wrapper, channel copy, buffered copy, item-count scale points `0/1/10/1,000/100,000`, first-item
  latency, full enumeration, and the custom allocated-bytes-per-item metric.
- The same stream suite now also includes the missing manual-iterator-copy strategy and the existing
  cancellation-latency benchmark class.
- `AnalyzerPerformanceBenchmarks` covers the BM012 analyzer matrix across `NoDiagnostics` and
  `ManyDiagnostics` source modes, strict architecture rules off/on, and cancellation scan off/on.
- `ObjectDispatchBenchmarks` isolates the extra boxing and switch path for `SendObject` without
  widening the core mediator abstractions.
- The shared `BenchmarkOutputConfig` adds explicit `Median`, `P95`, `P99`, and `Conditions` columns
  so those values are present in every benchmark summary instead of depending on BenchmarkDotNet's
  heuristics.
- `DispatchScaleBenchmarks` covers class, record class, struct, and readonly record struct request
  forms.
- The dispatch scale suite varies request count across `1`, `10`, `100`, `1,000`, and `5,000`
  generated request types for each covered request shape.
- The same suite now compares candidate lookup strategies for generic dispatch:
  static generic cache, mutable dictionary, and `FrozenDictionary`.
- `PipelineDispatchBenchmarks` already covers the BM004 pipeline matrix across `0`, `1`, `3`, `5`,
  and `10` behaviors, plus delegate-chain, static-delegate-chain, generated-nested-call, and
  chain-object strategy comparisons.
- The dispatch scale suite now also varies pipeline count across `0`, `1`, `3`, and `10`, with the
  non-zero variants executing real generated-style pipeline chains around the handler call.
- `ApiShapeBenchmarks`, `ObjectDispatchBenchmarks`, `PipelineDispatchBenchmarks`, and
  `DispatchScaleBenchmarks` use `[DisassemblyDiagnoser(maxDepth: 0)]` so those suites report JIT
  code size alongside timing statistics. `maxDepth: 0` limits disassembly to the benchmark method
  itself, not its callees, keeping output focused on the dispatch entry path and preventing
  excessive output or run-time overhead from callee expansion.
- `DiscoveryBenchmarks`, `NotificationPublishGenerationBenchmarks`, and `DispatchScaleBenchmarks`
  report generated source size through benchmark-specific summary columns for the generator-focused
  slices.
- All benchmark classes keep BenchmarkDotNet's memory diagnoser enabled, so allocated bytes plus
  `Gen0`/`Gen1`/`Gen2` stay visible in every summary.

## See Also

- [SharedKernel.Mediator.SourceGenerator](../../src/Mediator/SharedKernel.Mediator.SourceGenerator/README.md)
