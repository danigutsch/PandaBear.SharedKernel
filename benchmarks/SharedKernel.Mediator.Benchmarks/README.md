# SharedKernel.Mediator.Benchmarks

Benchmark harness for the `SharedKernel.Mediator` discovery generator.

## Scope

- Measure discovery-report generation across the M03 scale points: 10, 100, 1,000, and 5,000 requests
- Compare clean-build, no-op rebuild, and one-handler-edit rebuild scenarios
- Capture generator allocation impact with BenchmarkDotNet's memory diagnoser
- Track generated source length for the baseline and edited inputs inside the benchmark fixture

## Run

```powershell
dotnet run --project benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj -c Release -- --filter *DiscoveryBenchmarks*
```

## Notes

- The benchmark sources are synthetic and generated in-memory to isolate discovery-pipeline cost.
- `Clean build` recreates the compilation from source before running the generator.
- `No-op rebuild` reuses the same compilation and generator graph.
- `One-handler edit rebuild` reruns the graph against a compilation whose first handler body changed.

## See Also

- [tmp/mediator.md](../../tmp/mediator.md)
- [SharedKernel.Mediator.SourceGenerator](../../src/Mediator/SharedKernel.Mediator.SourceGenerator/README.md)
