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
A consumer repository records its compatibility with a candidate by running its own build and tests on a clean clone of its committed revision
with `sharedkernel-version verify-consumer --candidate <candidate.json> -- <command>...`; the
[Versioning tool README](../tools/SharedKernel.Versioning.Tool/README.md#candidates) describes the checks and the evidence.
Linux and Windows consume the same hashed artifacts before stable promotion. Repository and Source Link metadata target the public repository; candidate validation records source and symbol evidence separately.


### Package requirements

Every packable project sets `PackageId` (`PandaBear.` plus the project name), `PackageTags`, and `Description`, ships a `README.md` next to the
project file, and packs it through `PackageReadmeFile`. `Directory.Build.props` supplies the MIT license expression, the repository URL, type,
and `PublishRepositoryUrl`, so packed metadata and Source Link data come from one place. Libraries whose project name starts with `SharedKernel.`
also pack a `snupkg` symbol package; Roslyn components (`IsRoslynComponent`) are excluded from symbol packages. Repository
convention tests in `tests/SharedKernel.Repository.Tests` fail the build when a packable project misses any of these. Packed `.nuspec` files record the repository commit through the SDK's Source Link support, with no extra package.

`EnablePackageValidation` stays off. Its baseline validator compares a package with a previously released stable version, and no stable version exists while all packages are preview. Enable it with `PackageValidationBaselineVersion` when the first stable release is authorized.

### Source generator packages

Source generators follow one contract:

- The package has a README that describes its diagnostics and generated output.
- Generated C# starts with the exact line `// <auto-generated />` (with a space before `/>`) followed by `#nullable enable`.
- Hint names end in `.g.cs`. A generator with one output per run uses a constant; a generator with one output per input type derives the name only from that type's metadata name. The generator emits sources and diagnostics in ordinal order so output is deterministic.
- Generated code carries no source or input attribution: no timestamps, absolute paths, machine names, or tool versions. The hint name and the generated type names identify the generator and the input type, so the same inputs always produce the same bytes.
- Each generator owns one diagnostic ID prefix: `AUDIT` (audit trail), `SKMDL` (domain models), `SKMED` (mediator), and `SKMSG`
  (integration events). A generator that reports no diagnostics, such as the results error catalog, has no prefix and no
  `AnalyzerReleases` files. Shipped and unshipped IDs go in `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`.
- A generator that ships MSBuild assets adds them under `buildTransitive`. Only the mediator generator does; the others need none.
- Public generated API is documented or internal.
- Every generator has a package-consumption test that restores the packed package, builds a fresh project, and finds the generated file by its hint name: mediator and integration events in `SharedKernel.Mediator.PackageConsumptionTests`, domain and results in `SharedKernelGeneratorPackageConsumptionTests`, and audit trail in `SharedKernel.AuditTrail.GeneratorTests`. The results generator ships inside `PandaBear.SharedKernel.Results`, so a consumer references that package alone. The results generator has no standalone package.
- Each generator's tests assert the hint names of the files it emits, so a rename fails a test.

Convention tests check the README, the generated-file header, and `#nullable enable` for every generator project.

### Package-consumption tests

`tests/SharedKernel.Testing.Packaging` holds the generic helpers: `NuGetPackageBuilder`, `DotNetCli`, and
`PackageConsumptionWorkspace`, which writes consumer projects that restore only from a local feed. Put code in that project when it knows
nothing about one package. Add a package-specific consumer test project, such as `tests/SharedKernel.Mediator.PackageConsumptionTests`, when a
package needs its own feed contents, consumer project templates, or assertions on generated output; it creates workspaces from its feed
fixture and keeps those templates and assertions to itself. Add no CI lane for such a project unless its measured runtime justifies one.

For setup, coding, and NativeAOT prerequisites, see [development guidance](development.md). For preview versions and candidate policy, see [release policy](release-policy.md).
