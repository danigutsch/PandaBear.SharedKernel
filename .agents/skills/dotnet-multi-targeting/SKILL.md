---
name: dotnet-multi-targeting
description: Use when moving the repository to another .NET version, changing the global.json SDK or SharedKernelTargetFramework, when verify-target-frameworks reports a framework name outside Directory.Build.props, when a consuming repository must build more than one .NET version (TargetFrameworks, per-framework package versions, sharedkernel-version --framework), or for NETSDK1045, NU1004, NU1510, CP0002, CA1307 on char overloads, or "Your project targets multiple frameworks" errors.
license: See repository LICENSE.txt
---

# .NET versions and target frameworks

This repository builds one .NET version from one setting; the repository tools support repositories that build
several. Support for a version is proven, not declared: tests run on that runtime and a packed consumer on that
framework restores, builds, runs, and publishes NativeAOT. Read the touched flow, the nearest `AGENTS.md`, and
[.NET versions and target frameworks](../../../docs/dotnet-multi-targeting.md) first.

## This repository: moving to another .NET version

A change that moves the version contains each of these, or says why one does not apply:

1. **Setting**: `SharedKernelTargetFramework` in the root `Directory.Build.props` is the only place that names the
   framework. Never write a moniker in a project, props, targets, nuspec, workflow, action, script, or the policy file;
   use `$(SharedKernelTargetFramework)`. `-p:SharedKernelTargetFramework=…` builds another version without touching the
   `netstandard2.0` analyzers.
2. **SDK**: `global.json` major at least the framework's (NETSDK1045 otherwise); keep `rollForward: disable`.
3. **Packages**: move the packages with one line per .NET version to the new line, unless a dependency blocks it (the
   EF family waits for Npgsql's EF provider). Packages the new shared framework contains lose their direct references
   (NU1510).
4. **Code**: fix what the new analyzers report (CA1307 `char` overloads take `StringComparison.Ordinal`; CA2016 passes
   the token) and what the new runtime changes; never skip a test.
5. **Locks**: regenerate with `dotnet restore SharedKernel.slnx --force-evaluate` (NU1004 otherwise), then apply the
   lock retention rule in `docs/package-development.md`.
6. **Analysis**: a new SDK major changes what `AnalysisLevel=latest` means; fix new findings or pin the previous level
   in the SDK change.
7. **Release**: `!` in the title and a `BREAKING CHANGE:` footer; applications on the old version cannot take the
   packages.
8. **Evidence**:
   - `sharedkernel-repo verify-target-frameworks`
   - restore `--locked-mode`, build, and `dotnet test --solution SharedKernel.slnx`
   - `sharedkernel-version validate-candidate --aot`, which consumes the repository's framework by default

   Report what ran on which runtime.

The weekly `dotnet-preview` workflow builds the next version's preview. Read its failures before the release.

## A repository that builds several versions

Use the tools' multi-framework support rather than new scripts:

- **Policy**: `targetFrameworks` rules list monikers, or name a property from `frameworkProperties` whose `;` list is
  unique and ascending. The first matching rule wins, and `*` also matches `/`.
- **Projects**: where `TargetFrameworks` is set, clear the inherited value with `<TargetFramework></TargetFramework>`.
- **Package versions**: items hold the oldest framework's versions; each newer framework gets
  `<ItemGroup Condition="'$(TargetFramework)' == '<tfm>'">` with `PackageVersion Update=` items.
- **Package validation**: `EnablePackageValidation`, whose compatible-framework check (CP0002) needs no baseline.
- **Consumption**: `sharedkernel-version … --framework <a> --framework <b>`, one consumption per framework.
- **CI**: install the older runtimes beside the SDK. Every `dotnet run --project` of a multi-framework executable
  passes `--framework`.

## Common mistakes

| Mistake | Fix |
| --- | --- |
| Writing `net11.0` in a project, workflow, or test template | Use `$(SharedKernelTargetFramework)`, or in a test the framework it was built for (`TestTargetFramework.Moniker`) |
| Moving EF Core to a new major before Npgsql's EF provider supports it | Keep the EF family on the old line; EF Core 11 breaks code built against EF Core 10 |
| Shipping a dependency on a release candidate | Wait for the stable release |
| Guessing the latest SDK or package version | Check the dotnet/core release notes and nuget.org; cite them |
| `TargetFrameworks` on an Aspire AppHost-SDK project | It fails in the multi-framework outer build (MSB4036 `PickBestRid`); keep one framework |
| Treating a test that fails only on the new runtime as flaky | Find the runtime change (the guide lists known ones); fix the code or make the test data exact |
