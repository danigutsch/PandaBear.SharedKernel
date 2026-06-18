# SharedKernel.Functional.Benchmarks

Benchmark harness for `SharedKernel.Results` creation, conversion, and validation aggregation paths.

## Scope

- Measure non-generic and generic success creation
- Measure non-generic and generic failure creation
- Measure single-field and multi-field validation failure creation
- Measure invalid-result conversion between generic and non-generic forms
- Measure validation error aggregation into one structured failure
- Compare `Result<T>` success-path cost across class, record class, struct, readonly record struct, and a larger struct payload
- Compare payload-shape impact on `Result.Ok(T)`, `Map`, `Bind`, and `TryGetValue`
- Compare benchmark-only helper calls that pass a larger struct by value vs `in`

## Run

```powershell
dotnet run --project benchmarks/SharedKernel.Functional.Benchmarks/SharedKernel.Functional.Benchmarks.csproj -c Release -- --filter *ResultCreationBenchmarks*
dotnet run --project benchmarks/SharedKernel.Functional.Benchmarks/SharedKernel.Functional.Benchmarks.csproj -c Release -- --filter *ResultFailureFlowBenchmarks*
dotnet run --project benchmarks/SharedKernel.Functional.Benchmarks/SharedKernel.Functional.Benchmarks.csproj -c Release -- --filter *ResultValueShapeBenchmarks*
dotnet run --project benchmarks/SharedKernel.Functional.Benchmarks/SharedKernel.Functional.Benchmarks.csproj -c Release -- --filter *LargeStructInParameterBenchmarks*
```

## Current Findings

- `Result<T>` with the existing `readonly struct` carrier shape is already fast for normal payloads
- larger struct payloads make copy-sensitive reads more visible than small struct or reference payloads
- benchmark-only `in` helper calls help simple large-struct projection, but they do not consistently
  improve create-style or sink-style paths
- current data does not justify changing `Result`, `Result<T>`, or `Option<T>` to a different public
  type shape
