# Package development and validation


Install the SDK pinned in `global.json`. Provider integration tests require a working container runtime; NativeAOT checks also require the platform
native build toolchain.

Analyzer and source-generator packages retain the Roslyn 5.6 compiler API baseline so consumers can use compatible .NET 10 SDKs without upgrading
to this repository's development SDK. The repository SDK and runtime dependencies can advance independently of that compiler compatibility floor.

```sh
dotnet restore SharedKernel.slnx --locked-mode
dotnet build SharedKernel.slnx -c Release --no-restore
dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore
```

Coverage uses the MIT-licensed `coverlet.MTP` extension, which every test project references, so `sharedkernel-ci run
--coverage` gets a Cobertura report from each selected test project. To collect Cobertura for a project, run:

```sh
dotnet test --project tests/SharedKernel.HttpClients.Tests/SharedKernel.HttpClients.Tests.csproj -c Release --no-build --no-restore --coverlet --results-directory TestResults/coverage
```

`tests/testconfig.json` defines the coverage filters and is copied beside test executables. It excludes test assemblies, migrations, intermediate and
generated source files, and explicitly marked generated/excluded code. Compiler-generated async and iterator code remains measurable. Coverlet
measures instrumented managed modules in the test process; separate application/container processes are not covered automatically. Selected-project
reports are evidence for those projects, not whole-repository coverage trends. This repository does not currently enforce a coverage percentage.

Build gates retain SDK analyzers (`AnalysisMode=All`), SharedKernel analyzers, warnings as errors, public API checks, dependency vulnerability auditing,
and package-consumption checks. No remote SonarCloud upload or proprietary coverage collector is required.

`SharedKernel.CI.Tool` packs as the `sharedkernel-ci` tool and also builds from a sparse checkout of its own directory
and `tools/Shared`. Selection follows `.config/sharedkernel-ci.json`: it selects the test projects that depend on the
changed projects, writes one solution filter per test slice, and selects every test project when history, a path, or a
reference is uncertain. A library or tool change selects only the test projects that depend on it, and still runs the
candidate job, which packs and consumes every package. Full runs happen nightly; selection reasons are retained in CI.

```sh
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- select --base <base-object-id>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- run --slice library
```

Commit changes before creating candidate evidence. Candidates receive unique ordered prerelease versions and are never overwritten.
`SharedKernel.Versioning.Tool` validates APIs, packs once, records the library revision and SHA-256 package hashes, then installs every packed tool
and runs each command that its package declares with `--help`, and checks isolated package restore, locked restore, build, run, generators, and optional NativeAOT.
Candidate manifests mark unavailable consumer, baseline, and platform evidence as pending.

```sh
dotnet run --project tools/SharedKernel.Versioning.Tool -c Release -- validate-candidate --aot
dotnet run --project tools/SharedKernel.Versioning.Tool -c Release -- consume-candidate --candidate <candidate.json> --aot
```

Candidate packages and their manifest can be copied together to another machine; consumption regenerates configuration for the current feed location.
Linux and Windows consume the same hashed artifacts before stable promotion. Repository and Source Link metadata target the public repository; candidate validation records source and symbol evidence separately.


For setup, coding, and NativeAOT prerequisites, see [development guidance](development.md). For preview versions and candidate policy, see [release policy](release-policy.md).
