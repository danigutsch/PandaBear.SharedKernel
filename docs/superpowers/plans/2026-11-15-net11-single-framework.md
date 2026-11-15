# .NET 11 Only, From One Setting — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build every project of the repository for `net11.0` from one setting, and keep multi-framework support only in the repository tools.

**Architecture:** `SharedKernelTargetFramework` in the root `Directory.Build.props` feeds `TargetFramework`. The policy check (`sharedkernel-repo verify-target-frameworks`) resolves `$(…)` references from that file and rejects framework names anywhere else. The versioning tool defaults to the repository's framework. CI derives runtimes from the setting, and a preview workflow builds the next major.

**Tech Stack:** .NET SDK 11.0.100, MSBuild, NuGet central package management with lock files, System.CommandLine 3, xUnit v3 on Microsoft.Testing.Platform, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-15-single-framework-setting-design.md`

## Global Constraints

- Repository rules in `AGENTS.md`: file-scoped namespaces, one type per file, XML docs on public APIs, no `Async` suffix, warnings as errors, PublicAPI files, Conventional Commits, signed commits.
- Every commit ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Commands set `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1`. Run restore, build, test, and git outside the sandbox.
- The setting is `<SharedKernelTargetFramework>net11.0</SharedKernelTargetFramework>` with `<TargetFramework>$(SharedKernelTargetFramework)</TargetFramework>`.
- EF family stays 10.x: `Microsoft.EntityFrameworkCore.*`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.6.5.
- Roslyn packages 5.12.0, `Microsoft.Build.*` 18.12.2.
- Tests that feed made-up repositories to the tools keep literal framework names, including multi-framework ones.

## Review Focus

1. A policy rule that names a property missing from the `frameworkProperties` file must report the rule, not silently match nothing. Owned by Task 1.
2. A `TargetFramework` platform suffix (`net11.0-windows`) in a scanned file is a framework name and must be reported. Owned by Task 1.
3. A root `Directory.Build.props` whose framework property is conditional, or missing, must fail the versioning tool with a message that names the file, not consume nothing. Owned by Task 2.
4. The preview workflow must succeed with a notice when no preview of the next major exists. Owned by Task 3.
5. `-p:SharedKernelTargetFramework=…` must not retarget the `netstandard2.0` analyzers. Owned by Task 4, which builds an analyzer with it.

---

### Task 1: Policy check resolves the framework setting and rejects stray framework names

**Files:**
- Modify: `src/SharedKernel/SharedKernel.Repositories/TargetFrameworkConfiguration.cs`, `TargetFrameworkVerifier.cs`, `TargetFrameworkEvaluator.cs`, `ConsumerConfigurationFile.cs` (validation), `PublicAPI.Unshipped.txt`
- Create: `src/SharedKernel/SharedKernel.Repositories/FrameworkNameScanner.cs`
- Test: `tests/SharedKernel.RepoConfig.Tests/VerifyTargetFrameworksCommandTests.cs`, `TargetFrameworkTestRepository.cs`
- Docs: `tools/SharedKernel.RepoConfig.Tool/README.md`

**Interfaces:**
- Produces: `TargetFrameworkConfiguration(IReadOnlyList<TargetFrameworkRule> Projects, IReadOnlyList<string>? CommandFiles = null, string? FrameworkProperties = null)`, JSON `frameworkProperties`. `internal static IReadOnlyDictionary<string, string> TargetFrameworkEvaluator.EvaluateProperties(MsBuildFileSet files, string path)`. `internal static IReadOnlyList<(int Line, string Name)> FrameworkNameScanner.Scan(string text)`.

- [ ] **Step 1: Write the failing tests** in `VerifyTargetFrameworksCommandTests` (made-up repositories, literal names):
  - `A_rule_naming_a_property_matches_its_value`: props defines `<Fw>net11.0</Fw>`, rule `"frameworks": ["$(Fw)"]`, project builds `net11.0` → exit 0.
  - `A_rule_naming_an_undefined_property_is_reported`: rule `["$(Missing)"]` → exit 1, message contains `'$(Missing)'` and the props path.
  - `Property_references_need_frameworkProperties`: rule `["$(Fw)"]` without `frameworkProperties` → configuration error naming `$.targetFrameworks.projects[0].frameworks[0]`.
  - `A_framework_property_must_be_ascending_and_unique`: `<Fws>net12.0;net11.0</Fws>` referenced by a rule → issue on the props file; `net11.0;net11.0` → issue.
  - `A_framework_name_outside_the_setting_is_reported`: with `frameworkProperties` set, `tools/x.csproj` containing `<TargetFramework>net11.0</TargetFramework>`, `.github/workflows/ci.yml` containing `--framework net11.0`, and `build.sh` containing `net11.0-windows` → one issue each, with line numbers.
  - `Version_group_conditions_and_netstandard_are_allowed`: `Directory.Packages.props` with `Condition="'$(TargetFramework)' == 'net12.0'"` and a project with `netstandard2.0` → no stray-name issue.
  - `Without_frameworkProperties_literals_are_not_scanned`: literal rule and a literal in a workflow → exit 0.
- [ ] **Step 2: Run** `dotnet test --project tests/SharedKernel.RepoConfig.Tests -c Release --filter-class '*VerifyTargetFrameworksCommandTests'`. Expected: the new tests fail.
- [ ] **Step 3: Implement.**
  - Add `FrameworkProperties`, validated as a repository path. A rule framework is a moniker or `$(Name)`, and `$(Name)` requires `FrameworkProperties`.
  - `EvaluateProperties` processes one file with the existing evaluator and returns its properties.
  - `Verify` resolves `$(Name)` rule entries by splitting the value on `;`. It checks each referenced property's list for uniqueness and ascending versions, and the SDK check applies to the resolved frameworks.
  - When `FrameworkProperties` is set, it scans the tracked `*.csproj`, `*.fsproj`, `*.vbproj`, `*.props`, `*.targets`, `*.nuspec`, `.github/**/*.yml`, `.github/**/*.yaml`, `*.sh`, `*.ps1`, and the policy file itself. It skips the `FrameworkProperties` file and `'$(TargetFramework)' == '…'` conditions.
  - `FrameworkNameScanner` matches `\bnet\d+\.\d+(-[a-z0-9.]+)?\b`, not `netstandard` or `netcoreapp`. Each match is reported as "Names .NET framework '<name>'; use $(<property>) from <file>".
- [ ] **Step 4: Run** the same tests; expected PASS. Run the whole `SharedKernel.RepoConfig.Tests` project; expected all pass.
- [ ] **Step 5: README** — document `frameworkProperties`, `$(…)` references, and the stray-name check.
- [ ] **Step 6: Commit** `feat(repositories): resolve framework properties and reject stray framework names in verify-target-frameworks`.

### Task 2: Versioning tool consumes the repository's framework by default

**Files:**
- Create: `src/SharedKernel/SharedKernel.Versioning/RepositoryFrameworks.cs`
- Modify: `ConsumerFrameworks.cs` (drop `Default`), `PackageConsumptionOptions.cs`, `CandidateConsumptionOptions.cs`, `CandidateOptions.cs`, `CandidateValidator.cs`, `PackageConsumption.cs`, `SharedKernelLocalFeed.cs`, `PublicAPI.Unshipped.txt`, `tools/SharedKernel.Versioning.Tool/VersioningCommandLine.cs`, `tools/SharedKernel.Versioning.Tool/README.md`
- Test: `tests/SharedKernel.Versioning.Tests/RepositoryFrameworksTests.cs` (new), `ConsumerFrameworkTests.cs`, `PackageConsumptionProjectTests.cs`, and the fixture that writes candidate repositories (`InventoryFiles.cs` / `TemporaryReleasePrepDirectory`)

**Interfaces:**
- Produces: `public static class RepositoryFrameworks { public static IReadOnlyList<string> Read(string repoRoot); }`, which reads `<repoRoot>/Directory.Build.props`, returns `TargetFrameworks` (split on `;`) or else `TargetFramework`, and resolves `$(Name)` from properties in the same file. It throws `InvalidOperationException` naming the file when the file is missing, when the property is missing or conditional, or when the value is not a moniker. Options' `Frameworks` becomes `IReadOnlyList<string>?`, where `null` means the repository's frameworks. `SharedKernelLocalFeed.WriteRestoreProject(string restoreDirectory, string[] packageIds, string version, string framework)`.

- [ ] **Step 1: Write the failing tests** in `RepositoryFrameworksTests`:
  - `Reads_a_single_framework_through_a_property`: `<A>net11.0</A><TargetFramework>$(A)</TargetFramework>` → `["net11.0"]`.
  - `Reads_a_framework_list`: `<TargetFrameworks>net11.0;net12.0</TargetFrameworks>` → `["net11.0", "net12.0"]`.
  - `A_missing_file_or_property_names_the_file`.
  - `A_conditional_property_is_rejected`.

  In `ConsumerFrameworkTests`, replace `Frameworks_default_to_net10` with `Without_framework_the_option_is_empty`, keep the repeated-option and validation tests, and add `Consumption_without_framework_uses_the_repository_framework`: the fixture's `Directory.Build.props` says `net11.0` and evidence records `["net11.0"]`.
- [ ] **Step 2: Run** `dotnet test --project tests/SharedKernel.Versioning.Tests -c Release --filter-class '*RepositoryFrameworksTests' --filter-class '*ConsumerFrameworkTests'`; expected FAIL.
- [ ] **Step 3: Implement.**
  - `PackageConsumption.Run` and `CandidateValidator.Consume` use `options.Frameworks ?? RepositoryFrameworks.Read(root)`, and evidence records the resolved list.
  - The CLI option has no default value.
  - `SharedKernelLocalFeed.WriteRestoreProject` takes the framework from its caller, which reads the repository.
  - The candidate fixture writes `Directory.Build.props` with the framework of the running test (`new FrameworkName(AppContext.TargetFrameworkName!)` → `net{Major}.{Minor}`).
  - `PackageConsumptionProjectTests` reads that framework's folder.
- [ ] **Step 4: Run** the whole `SharedKernel.Versioning.Tests` project; expected all pass.
- [ ] **Step 5: README** — `--framework` defaults to the repository's framework.
- [ ] **Step 6: Commit** `feat(versioning): consume the repository's framework unless --framework is given`.

### Task 3: CI derives runtimes from the setting; preview workflow

**Files:**
- Modify: `.github/actions/setup-dotnet-tool/action.yml`, `.github/workflows/ci.yml`, `tests/SharedKernel.Repository.Tests/WorkflowContractTests.cs` (and any convention test that lists workflows or pins), `.github/action-pins.json` if a new action is used
- Create: `.github/workflows/dotnet-preview.yml`

**Interfaces:**
- Consumes: the setting's property name `SharedKernelTargetFramework`, and the `global.json` SDK version.

- [ ] **Step 1: Write the failing contract tests:**
  - `Workflows_name_no_framework_or_runtime_version`: no `--framework` and no `dotnet-version:` in `ci.yml`.
  - `The_setup_action_installs_the_runtime_of_an_older_framework`: the action's script reads `SharedKernelTargetFramework` with `dotnet msbuild Directory.Build.props -getProperty:SharedKernelTargetFramework` and compares majors.
  - `The_preview_workflow_is_manual_and_scheduled_and_not_required`: `on.schedule` and `on.workflow_dispatch`, read-only permissions, and pinned actions.
- [ ] **Step 2: Run** `dotnet test --project tests/SharedKernel.Repository.Tests -c Release --filter-class '*WorkflowContractTests'`; expected FAIL.
- [ ] **Step 3: Implement.**
  - Remove the 7 `dotnet-version: 10.0.x` entries and the 3 `--framework` pairs. The direct `actions/setup-dotnet` uses in `ci.yml` (source-build, repository-quality) switch to the shared action where they install from `global.json`.
  - The action's bash step: `fw=$(dotnet msbuild Directory.Build.props -getProperty:SharedKernelTargetFramework)`, `sdk=$(dotnet --version)`. When `${fw#net}` major < sdk major, it runs a second pinned `actions/setup-dotnet` with `dotnet-version: <major>.0.x`, via an output and a conditional step.
  - `dotnet-preview.yml`:
    - Read the next major as the `global.json` SDK major + 1.
    - Query `https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json` for that channel. Without it, write a notice and exit 0.
    - Otherwise run `setup-dotnet` with `dotnet-version: <next>.0.x` and `dotnet-quality: preview`, remove `global.json`, then restore, build, and test with `-p:SharedKernelTargetFramework=net<next>.0 -p:RestorePackagesWithLockFile=false`.
- [ ] **Step 4: Run** the contract tests and `actionlint` if installed; expected PASS.
- [ ] **Step 5: Commit** `ci: derive runtimes from the framework setting and build the next .NET preview weekly`.

### Task 4: Retarget the repository to the setting and net11.0

**Files:** root `Directory.Build.props`, `Directory.Build.targets` (C# 15 for `netstandard2.0`), `src/Directory.Build.targets` (remove the package-validation trigger), `tests/Directory.Build.props`, `tools/Directory.Build.props`, the 27 library `.csproj` files, `ImageProcessing.csproj`, the 4 PostgreSQL `.csproj` files, `Directory.Packages.props`, `SharedKernel.AuditTrail.SourceGenerator.nuspec` and `.csproj` (`targetFramework` nuspec property), the 4 `#if` files, the tests that describe this repository (`PackageConsumptionWorkspace.cs`, `SharedKernelMediatorPackageConsumptionTests.cs`, `SharedKernelGeneratorPackageConsumptionTests.cs`, `CodeFixRunnerTestProject.cs`), `.config/sharedkernel-repo.json`, every `packages.lock.json`.

- [ ] **Step 1: Set the setting** and remove the multi-framework project logic (see the spec's list).
- [ ] **Step 2: Packages.**
  - Fold the `net11.0` group into the items, keeping the EF family on 10.x.
  - Remove direct references to the 9 packages in `Microsoft.NETCore.App` 11 and their now-unused `PackageVersion` items.
  - Set Roslyn to 5.12.0 and MSBuild to 18.12.2.
  - Try `Microsoft.Data.Sqlite.Core` 11.0.0 and keep it if the Sqlite tests pass.
- [ ] **Step 3: Code.** Remove the `#if NET11_0_OR_GREATER` branches, keeping the .NET 11 side. Make the tests that describe this repository take the framework from `AppContext.TargetFrameworkName`. Point the nuspec at `$targetFramework$`.
- [ ] **Step 4: Policy.**
  - `.config/sharedkernel-repo.json` `targetFrameworks` sets `"frameworkProperties": "Directory.Build.props"`.
  - The rules become: the `netstandard2.0` ones, CodeFixRunner `LatestMajor`, `tools/*` `Major`, and `*.csproj` → `["$(SharedKernelTargetFramework)"]`.
  - Re-apply `lockExemptProjects` per the retention rule.
- [ ] **Step 5: Restore** with `dotnet restore SharedKernel.slnx --force-evaluate`, then build with `dotnet build SharedKernel.slnx -c Release --no-restore`. Expected: 0 warnings, 0 errors. Fix NU1510, NU1608, and audit findings. Drop pins that the audit no longer needs.
- [ ] **Step 6: Review-focus check.** Run `dotnet build src/SharedKernel/SharedKernel.Style.Analyzers -c Release -p:SharedKernelTargetFramework=net10.0 --no-restore`. Expected: it still builds `netstandard2.0`, with output in `bin/Release/netstandard2.0`.
- [ ] **Step 7: Commit** with a breaking change footer:

```
feat!: build net11.0 only, from one framework setting

BREAKING CHANGE: packages target net11.0 only; applications on .NET 10 cannot take this version.
```

### Task 5: Docs and skill

- [ ] Rewrite `docs/dotnet-multi-targeting.md`:
  - this repository builds one framework from `SharedKernelTargetFramework`;
  - moving it is one edit plus `dotnet restore --force-evaluate`;
  - the tools support multi-framework repositories, with the recipe (`TargetFrameworks` pattern, per-framework version groups, package validation, per-framework consumption);
  - the .NET 11 findings stay.
- [ ] Update `.agents/skills/dotnet-multi-targeting/SKILL.md`, `docs/development.md` (no .NET 10 runtime), `docs/package-development.md` (no validation trigger, Roslyn 5.12), `src/SharedKernel/SharedKernel.Testing.AspNetCore/README.md` if it mentions .NET 10, and `docs/README.md`.
- [ ] Run `dotnet run --project tools/SharedKernel.Documentation.Tool -c Release --no-build -- verify --config docs/architecture/generated-diagrams.json` and the Markdown link check if configured.
- [ ] Commit `docs: describe the single framework setting and the tools' multi-framework support`.

### Task 6: Full validation and PR

- [ ] Run, outside the sandbox:
  - `dotnet restore SharedKernel.slnx --locked-mode`
  - the Release build
  - `dotnet test --solution SharedKernel.slnx -c Release --no-build --no-restore`
  - the explicit governed-literal test
  - `sharedkernel-repo verify-target-frameworks`, `verify-json`, `verify-line-endings`, `verify-text-encoding`, and `verify-action-pins`
  - `sharedkernel-version verify-lock-coverage`
  - `dotnet format --verify-no-changes`
  - `dotnet list package --vulnerable --include-transitive`
  - the diagram check
  - `sharedkernel-version validate-candidate --aot`, with no `--framework`

  Expected: all pass, and the candidate is consumed as `net11.0`.
- [ ] Push `feat/dotnet-11`. Retitle PR #396 `feat!: target .NET 11 only, from one framework setting` and rewrite its description from the spec. Watch CI.
