# SharedKernel.Mediator.Benchmarks

Benchmark harness for the `SharedKernel.Mediator` discovery generator and API-shape experiments.

## Scope

- Measure discovery-report generation across the M03 scale points: 10, 100, 1,000, and 5,000 requests
- Compare clean-build, no-op rebuild, and one-handler-edit rebuild scenarios
- Capture generator allocation impact with BenchmarkDotNet's memory diagnoser
- Track generated source length for the baseline and edited inputs inside the benchmark fixture
- Compare direct `ValueTask<T>` and benchmark-only `Task<T>` handler returns
- Cover class, record class, and readonly record struct requests
- Cover synchronous and asynchronous completion paths
- Compare generated-style and hand-written mediator DI service-provider build costs
- Measure handler, mediator, and first-dispatch DI costs
- Measure generated object-switch dispatch separately from typed and generic dispatch
- Measure direct, typed, generic-switch, and object-switch dispatch across request-count scale points

## Run

```powershell
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DiscoveryBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *ApiShapeBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DependencyInjectionBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *ObjectDispatchBenchmarks*
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DispatchScaleBenchmarks*
```

## Notes

- The benchmark sources are synthetic and generated in-memory to isolate discovery-pipeline cost.
- `Clean build` recreates the compilation from source before running the generator.
- `No-op rebuild` reuses the same compilation and generator graph.
- `One-handler edit rebuild` reruns the graph against a compilation whose first handler body changed.
- `ApiShapeBenchmarks` isolates handler return-path shape only; the `Task<T>` path exists for
  benchmark comparison and is not part of the production mediator contract.
- `DependencyInjectionBenchmarks` uses a generated-style benchmark mediator shell so DI build,
  resolution, and first-dispatch costs stay close to the current generated shape.
- `ObjectDispatchBenchmarks` isolates the extra boxing and switch path for `SendObject` without
  widening the core mediator abstractions.
- `DispatchScaleBenchmarks` fixes the current request shape to class/no-pipeline dispatch and scales
  only the number of generated request types so the request-count rows in the checklist stay
  isolated from later shape and pipeline variables.

## See Also

- [tmp/mediator.md](../../tmp/mediator.md)
- [SharedKernel.Mediator.SourceGenerator](../../src/Mediator/SharedKernel.Mediator.SourceGenerator/README.md)
