# .NET 11 only, from one setting; multi-framework support in the tools

Date: 2026-11-15. Changes PR #396 (`feat/dotnet-11`), which built `net10.0;net11.0`.

## Intent

The repository's libraries, tests, samples, and tools build one .NET version, `net11.0`, named in one place, so .NET 11
APIs can be used without `#if` and moving to the next version is a one-line change. The projects carry no
multi-framework logic. Multi-framework support lives in the repository tools (`sharedkernel-repo`,
`sharedkernel-version`, `sharedkernel-ci`), which consuming repositories, or this one later, can use.

The owner's rules:

- Every consuming application can move to .NET 11, at least its target framework.
- A package keeps an older major version when that version works on `net11.0` and nothing requires the newer one.
- The framework is named in a single location.

## Goals

- `SharedKernelTargetFramework` in the root `Directory.Build.props` names the framework. No other project, build, CI,
  tool-default, or configuration file names a .NET framework.
- The repository builds, tests, packs, and consumes `net11.0` only.
- The tools keep, and test, their multi-framework support: per-framework policy checks and lock sections, per-framework
  package consumption and evidence, per-framework central package versions, multi-framework output folders, and
  per-framework governed-literal manifests.
- A check fails when a framework name is written outside the setting.
- The next .NET version's breaking changes show up during its previews.

## Non-goals

- Moving EF Core to 11 (waits for `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0 stable).
- Multi-targeting any project of this repository.
- Adopting .NET 11 APIs beyond removing the `#if` branches.

## Constraints and facts

- .NET 10 and .NET 11 support both end on 2028-11-14. Dropping .NET 10 shortens nothing; consumers still targeting
  .NET 10 cannot take later package versions.
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 requires EF Core `[10.0.4, 11.0.0)`; its newest version,
  `11.0.0-rc.3`, requires exactly EF Core `11.0.0-rc.3.26522.148` (nuspecs checked 2026-11-15).
- EF Core 10 targets .NET 10, is supported until 2028-11-10, and works from `net11.0`. Verified 2026-11-15: the
  PostgreSQL tests pass on the .NET 11.0.0 runtime with EF Core 10 and Npgsql 10.0.3, and
  `SharedKernel.EntityFrameworkCore` builds for `net11.0` against EF Core 10.0.15 with no warnings.
- `Microsoft.NETCore.App` 11 contains the `Microsoft.Extensions.*.Abstractions` packages for caching, configuration,
  dependency injection, diagnostics, file providers, hosting, and logging, plus `Options` and `Primitives`.
- SDK 11.0.100 ships Roslyn 5.12 and MSBuild 18.12.2; `Microsoft.CodeAnalysis.*` 5.12.0 and `Microsoft.Build.*`
  18.12.2 are on nuget.org.
- The Aspire AppHost SDK 13.6.5 fails in a multi-framework build (MSB4036). With one framework that no longer applies.

## Approaches

- **One setting, single framework in the projects (chosen).** Projects inherit the framework; the tools hold the
  multi-framework logic and read the setting.
- **One setting with the multi-framework project logic kept** (`TargetFrameworks` lists, per-framework version groups,
  package validation trigger). Rejected by the owner: the projects should not carry multi-framework logic.
- **Search and replace `net10.0` with `net11.0`.** Rejected: the next version change would touch about 80 files again.

## Design

### The setting

The root `Directory.Build.props` sets `SharedKernelTargetFramework` to `net11.0` and `TargetFramework` to
`$(SharedKernelTargetFramework)`. Every project inherits it. Only analyzers, code fixes, and generators set their own
`TargetFramework`, `netstandard2.0`, which is not a .NET version. A named property, not `TargetFramework` alone, lets
CI build another version with `-p:SharedKernelTargetFramework=…` without overriding those `netstandard2.0` projects.

Removed from the projects: `SharedKernelTargetFrameworks`, every `TargetFrameworks` list and `TargetFramework`
clearing (27 libraries, `tests/Directory.Build.props`, `tools/Directory.Build.props`), the explicit `net10.0` of
`ImageProcessing` and the four PostgreSQL projects, the `net11.0` group in `Directory.Packages.props`, and the
multi-framework package validation trigger in `src/Directory.Build.targets`. Tools keep `RollForward` (`Major`, and
`LatestMajor` for the code-fix runner).

### Packages (owner's rule)

- ASP.NET Core, `Microsoft.Extensions.*`, and `System.Security.*` move to 11.0.0, and `Microsoft.OpenApi` to 3.11.0.
- The EF family stays on 10.x: every package whose 11.x requires EF Core 11. Today that is `Microsoft.EntityFrameworkCore.*`,
  `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`,
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, and `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.6.5.
  `Microsoft.Data.Sqlite.Core` moves to 11.0.0 if the EF Core 10 Sqlite tests pass with it (the EF provider requires
  `>= 10.0.15`); otherwise it stays.
- Direct references to the packages that `Microsoft.NETCore.App` 11 contains are removed; they drop out of our packages'
  dependencies.
- Analyzers, code fixes, and generators move to Roslyn 5.12.0 and the code-fix runner to MSBuild 18.12.2. Their C#
  version becomes 15, the default of `net11.0`.
- Transitive security pins are re-checked with the NuGet audit; pins that 11.0.0 makes unnecessary go.
- Lock files are regenerated, and the minimal lock retention rule is re-applied.

### Code

The `#if NET11_0_OR_GREATER` branches go, keeping the .NET 11 side, in `ServerFormSubmission`, `UniqueRuleResolver`,
`ServerValidationBinderTests`, and `MultipartFormRequestBodyDocumentTransformerTestsHelpers`. `UniqueRuleResolver`'s
.NET 11 side works with EF Core 10 and 11. `GovernedConstantGuardTests` keeps `NET10_0`, a symbol of a made-up
compilation.

### Tools

- **`sharedkernel-repo verify-target-frameworks`:**
  - Policy rules may name a property, such as `"frameworks": ["$(SharedKernelTargetFramework)"]`, resolved from a new
    `frameworkProperties` file (`Directory.Build.props` here). Literal monikers stay valid.
  - With `frameworkProperties` set, it also requires each framework property's value to be unique, ascending, and
    buildable by the pinned SDK, and it reports any .NET framework name in a tracked project, props, targets, nuspec,
    workflow, action, or script file, or in the policy file, except in the `frameworkProperties` file and in
    `'$(TargetFramework)' == '…'` version-group conditions. Lock files, Markdown, and C# files are not scanned.
  - The policy file shrinks to the `netstandard2.0` rules, the tools' `RollForward`, and a default rule naming
    `$(SharedKernelTargetFramework)`.
- **`sharedkernel-version`:** without `--framework`, the consumption commands use the repository's framework, read
  from its root `Directory.Build.props` (`TargetFrameworks`, else `TargetFramework`, resolving `$(…)` within that file).
  A repeated `--framework` stays for multi-framework repositories, as do the per-framework evidence keys and
  `CentralPackageVersions`' version groups. `ConsumerFrameworks.Default` goes. `SharedKernelLocalFeed`'s restore
  project and the generator package's `lib/` folder and dependency group use the repository's framework.
- **`sharedkernel-ci`:** keeps the multi-framework Playwright installer lookup; the governed-literal guard keeps
  checking each framework's manifest.
- Tests that describe this repository take the framework from the build; tests that give made-up repositories to the
  tools keep literal names, including multi-framework ones.

### CI

- No `--framework` flags and no `dotnet-version: 10.0.x` entries.
- The shared `setup-dotnet-tool` action, after installing the SDK from `global.json`, installs the runtime of the
  repository's framework when it is older than the SDK. Jobs that install the SDK directly use the action instead.
- A `dotnet-preview` workflow (weekly and on demand) finds the next major after the pinned SDK. If a preview of it
  exists, it installs it and builds and tests with `SharedKernelTargetFramework=net<next>.0`, without lock files.
  Without a preview it reports that and succeeds. It is not required for merging.

### Release

The change is breaking for consumers on .NET 10: the PR title takes `!` and the commit a `BREAKING CHANGE:` footer,
which the versioning tool reads.

### Docs

`docs/dotnet-multi-targeting.md` becomes: this repository builds one framework from one setting; moving it is one edit
plus regenerated lock files; the tools support repositories that build several frameworks, with the recipe they need
(the `TargetFrameworks` pattern, per-framework version groups, package validation, per-framework consumption). The
`dotnet-multi-targeting` skill, `docs/development.md`, `docs/package-development.md`, and the tool READMEs follow.

## Left to the builder

- The exact policy field names and messages.
- Which tests describe this repository and which use made-up repositories, file by file.
- How the CI action and the preview workflow read the setting and the SDK major.
- Whether `Microsoft.Data.Sqlite.Core` moves to 11.
