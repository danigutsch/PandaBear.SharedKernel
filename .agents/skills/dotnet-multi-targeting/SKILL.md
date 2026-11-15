---
name: dotnet-multi-targeting
description: Use when a .NET project, package, test, or dotnet tool must support more than one .NET version (such as net10.0 and net11.0), when adding, moving, or dropping a target framework or the global.json SDK, when TargetFrameworks seems ignored, or for NETSDK1045, NU1004, NU1510, CP0002, RS0016/RS0017 per framework, or "Your project targets multiple frameworks" errors.
license: See repository LICENSE.txt
---

# Multi-targeting .NET versions

Support for a .NET version is proven, not declared: tests run on that runtime and a packed consumer on that TFM
restores, builds, runs, and publishes NativeAOT. A second TFM is one way to get there, not the default. Read the touched
flow, the nearest `AGENTS.md`, and [Supporting .NET 10 and .NET 11](../../../docs/dotnet-multi-targeting.md) first.

## Decide per project

| Project | TFMs |
| --- | --- |
| Library whose dependencies are the same for every major | lowest supported (`net10.0`); it runs on later runtimes |
| Library with per-major dependencies (ASP.NET Core, EF Core, Npgsql EF, `Microsoft.Extensions.*`) or a new-runtime API | `net10.0;net11.0`, once every dependency has a stable 11.x |
| Analyzer, code fix, generator | `netstandard2.0`, unchanged |
| Test project | every supported TFM, so each library runs on each runtime |
| dotnet tool | lowest TFM plus `RollForward=Major` (`LatestMajor` if it loads the SDK's MSBuild in process) |
| App, host, sample, benchmark | one TFM |

State the chosen row for each project in the change description.

## Required parts of a TFM change

A change that adds, moves, or drops a TFM or the SDK pin contains each of these, or says why one does not apply:

1. **Policy**: the `targetFrameworks` rules in `.config/sharedkernel-repo.json` (the first rule whose `include` matches
   wins; `*` also matches `/`), then
   `dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release -- verify-target-frameworks` until it passes.
2. **SDK**: `global.json` major at least the highest TFM (NETSDK1045 otherwise); keep `rollForward: disable`.
3. **Declaration**: TFMs come from one property per project kind. Where `TargetFrameworks` is set, clear the inherited
   value with `<TargetFramework></TargetFramework>`; otherwise the project silently builds one TFM.
4. **Packages**: a package with one line per .NET major (ASP.NET Core, EF Core, `Microsoft.Extensions.*`, `System.*`)
   uses the major of the TFM being built, whatever its support length: defaults hold the lowest TFM's versions, and
   `<ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">` overrides them with `PackageVersion Update=...`.
   Do not defer this; one version for both TFMs ships the wrong major to one of them.
5. **Code**: `#if NET11_0_OR_GREATER` with an equivalent `net10.0` branch; MSBuild conditions use
   `IsTargetFrameworkCompatible`. The public API is the same on every TFM.
6. **API checks**: shared `PublicAPI.*.txt` when the surface is identical; otherwise
   `AdditionalFiles Include="PublicAPI/$(TargetFramework)/PublicAPI.Shipped.txt"` (and `Unshipped`).
   `EnablePackageValidation` on multi-targeted libraries: its compatible-framework check needs no baseline and fails
   the pack with CP0002 when one asset lacks another's API.
7. **Locks**: a TFM change, or an SDK change that moves an implicit package such as `Microsoft.NET.ILLink.Tasks`,
   fails `--locked-mode` with NU1004. Regenerate with `dotnet restore SharedKernel.slnx --force-evaluate`, then re-run
   the lock retention rule in `docs/package-development.md`.
8. **Analysis**: a new SDK major changes what `AnalysisLevel=latest` means. If the build then reports new analyzer
   findings, pin the previous level in the SDK change and fix the findings in their own change.
9. **CI and tooling**: `setup-dotnet` lists the older runtime (`dotnet-version: 10.0.x`) beside `global-json-file`;
   packed consumers and NativeAOT run per TFM and platform (`PackageConsumptionCommand.cs` writes `net10.0` today);
   every `dotnet run --project` of a multi-targeted executable passes `--framework`.
10. **Evidence**: restore `--locked-mode`, build, `dotnet test --solution SharedKernel.slnx` (runs every TFM of each
    test project, as CI's `sharedkernel-ci run --slice` does), and the per-TFM pack and consumer checks. Narrow a local
    run with `dotnet test --project <test project> --framework net11.0`. Report what ran on which runtime.

## Common mistakes

| Mistake | Fix |
| --- | --- |
| Adding `net11.0` to every library "to support .NET 11" | `net10.0` assets run on .NET 11; prove it with tests and consumers, multi-target only per the table |
| Shipping a dependency on a release candidate to unblock a TFM | Keep that library on the old TFM until the dependency is stable |
| Guessing the latest SDK or package version | Check the dotnet/core release notes and nuget.org; cite them |
| Skipping the older runtime's build because it compiled once | Language and API features can fail only there; CI must build and test it |
