# SharedKernel.Results.Benchmarks

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
- Compare existing Result and Option storage with C# 15 declared unions

## Run

```powershell
dotnet run --project benchmarks/SharedKernel.Results.Benchmarks/SharedKernel.Results.Benchmarks.csproj -c Release -- --filter *ResultCreationBenchmarks*
dotnet run --project benchmarks/SharedKernel.Results.Benchmarks/SharedKernel.Results.Benchmarks.csproj -c Release -- --filter *ResultFailureFlowBenchmarks*
dotnet run --project benchmarks/SharedKernel.Results.Benchmarks/SharedKernel.Results.Benchmarks.csproj -c Release -- --filter *ResultValueShapeBenchmarks*
dotnet run --project benchmarks/SharedKernel.Results.Benchmarks/SharedKernel.Results.Benchmarks.csproj -c Release -- --filter *LargeStructInParameterBenchmarks*
```

## Current Findings

- `Result<T>` with the existing `readonly struct` carrier shape is already fast for normal payloads
- larger struct payloads make copy-sensitive reads more visible than small struct or reference payloads
- benchmark-only `in` helper calls help simple large-struct projection, but they do not consistently
  improve create-style or sink-style paths
- current data does not justify changing `Result`, `Result<T>`, or `Option<T>` to a different public
  type shape

## C# 15 union evaluation

Keep the existing public `Result`, `Result<T>`, and `Option<T>` types. C# unions are
a viable choice for new internal workflows with a closed set of structurally distinct
outcomes. The current public types already encode the behavior consumed by application
handlers, composition helpers, analyzers, and HTTP adapters.

| Concern | Current outcomes | C# 15 declared unions |
| --- | --- | --- |
| Consumption | `Match`, typed checks, and `TryGetValue` provide explicit two-way decisions. | A `switch` names each case directly, and adding a case makes existing exhaustive switches need attention. |
| Result meaning | Successful results retain statuses such as `Created` and `Accepted`. Failures retain a status and structured details. A default result is invalid for matching. | A success/failure declaration needs case wrappers to carry those statuses and an explicit null case to preserve default handling. |
| Option meaning | Defaults to none. Present values are non-null and may be value types. | A nullable case can express absence with a null match. A distinct case wrapper can preserve meaning when a domain needs more alternatives. |
| Composition | `Map`, `Bind`, validation, conversions, and Task or ValueTask composition have existing contracts. | Exhaustive matching is built in. Comparable composition and validation still need explicit APIs and compatible short-circuit behavior. |
| Storage and API compatibility | Typed fields and existing interfaces retain behavior and binary compatibility for consumers. | The declared representation holds object storage and boxes value-type cases. A custom union can use typed storage, but exposing its creation and matching surface still needs compatibility analysis. |

.NET SDK 11.0.100 compiled the union cases without a language preview setting. The C#
compiler matches union case types and supports a custom union pattern for alternative
storage. The benchmark measures declared unions. Adding a third case to the compiler
probe produced `CS8509` until the switch handled it.
[C# union reference](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/union)

The source-generated JSON probe, with reflection disabled, round-tripped the number,
string, and default cases as `42`, `"missing"`, and `null`. Two object-shaped cases
without a classifier failed source generation with `SYSLIB1227`. A nominal C# case
name does not create a JSON discriminator. Existing boundary DTOs and their generated
contracts should keep their established wire shapes.
[Union serialization](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/union-types)

The current `IResultLike<TSelf>` contract lets generic mediator behaviors inspect
the result status and construct failures without knowing the response type. The Results
analyzer resolves `Result` and `Result<T>` by metadata name, and the error catalog
generator recognizes those types by name. A public union replacement would need to
account for those callers and for the existing composition methods. C# 15 exposes union
cases to Roslyn for a future analyzer or generator that targets them.
[Result analyzer](../../src/SharedKernel/SharedKernel.Results.Analyzers/ResultsAnalyzer.cs) ·
[Error catalog generator](../../src/SharedKernel/SharedKernel.Results.SourceGenerator/ErrorCatalogModelBuilder.cs) ·
[C# 15 compiler APIs](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc1/csharp.md)

The comparison creates and reads batches of 256 integer outcomes with 75% success or
presence. Status and error data are carried in both Result representations. A cached
error keeps error construction outside the measurement, and a non-inlined producer
preserves the storage boundary. The benchmark checks that both representations return
the same matches. The workload measures synchronous construction and reads, with
results normalized per outcome.

Run the benchmark with the repository-pinned SDK and dependencies.

```sh
dotnet restore benchmarks/SharedKernel.Results.Benchmarks/SharedKernel.Results.Benchmarks.csproj --locked-mode
dotnet run --project benchmarks/SharedKernel.Results.Benchmarks -c Release --no-restore -- \
  --filter '*UnionOutcomeBenchmarks*' --inProcess --launchCount 1 --warmupCount 5 --iterationCount 8
```

BenchmarkDotNet 0.15.8 does not recognize .NET 11 in its child-process SDK validator.
Its in-process toolchain runs the comparison on the pinned runtime without a package
update. [BenchmarkDotNet command-line options](https://benchmarkdotnet.org/articles/guides/console-args.html)

### Measurements

The Release run on Linux x64 (AMD Ryzen AI 9 HX 370, .NET runtime 11.0.0) used one
benchmark process, five warmups, and eight measured iterations.

| Outcome | Mean per outcome | Managed allocation per outcome |
| --- | ---: | ---: |
| `Option<int>` | 1.425 ns | 0 B |
| Declared option union | 3.300 ns | 18 B |
| `Result<int>` | 2.272 ns | 0 B |
| Declared result union | 4.742 ns | 26 B |

BenchmarkDotNet reported short iterations for the two existing carriers. The timing
means describe this integer workload, and the allocation measurements show the cost
of declared union storage in it. Boxing present `int` cases allocates 18 B per option
on average at this mix. The declared result holds a struct case for each outcome,
including the failed case, which retains a status and the shared error reference.

Exhaustive matching improves the expression of certain internal state machines. Changing
the public outcome types would require preserving status, validation, equality, defaults,
composition, and caller interfaces while introducing a different pattern surface. Neither
the measured declared union nor the additional work of a custom union justifies such a
migration for the existing types.
