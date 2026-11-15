# Supporting .NET 10 and .NET 11

This guide says how the repository supports .NET 10 and .NET 11 at the same time: which projects build which target
framework (TFM), what each choice costs, and the order of the changes. Facts were checked on 2026-11-15 against the
sources at the end; items marked *verified locally* were reproduced with SDK 11.0.100 on Linux.

The [multi-targeting agent skill](../.agents/skills/dotnet-multi-targeting/SKILL.md) applies this guide to a change.
`sharedkernel-repo verify-target-frameworks` checks the result against the policy in `.config/sharedkernel-repo.json`
(see the [RepoConfig tool README](../tools/SharedKernel.RepoConfig.Tool/README.md)).

## The two releases

| | .NET 10 | .NET 11 |
| --- | --- | --- |
| Support | LTS, released 2025-11-11 | STS, released 2026-11-10 |
| End of support | 2028-11-14 | 2028-11-14 |
| First SDK | 10.0.100 | 11.0.100 (runtime 11.0.0) |
| Default C# | 14 | 15 |

STS releases are supported for 24 months since .NET 9, so both releases end on the same day. Supporting both costs no
extra support window; dropping `net10.0` early gains nothing either. .NET 12 (LTS) is due in November 2027 and will be
the next release to add.

## Support does not require multi-targeting

A `net10.0` assembly runs on the .NET 11 runtime. An application that targets `net11.0` restores the `net10.0` asset of
a package that has no `net11.0` asset. So "supports .NET 11" means three things, and only the third needs a second TFM:

1. **Runs on .NET 11**: tests execute on the .NET 11 runtime and pass.
2. **Consumable from `net11.0`**: a packed package restores, builds, runs, and publishes NativeAOT in a `net11.0`
   consumer.
3. **Ships a `net11.0` asset**: needed when the package's dependencies differ by major version, or when the code uses
   an API that only .NET 11 has.

Microsoft's [library guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/cross-platform-targeting)
says the same: consider multi-targeting when a project has package dependencies, and avoid it when the source is the
same and there are no dependencies.

## Policy by project kind

| Kind | Target frameworks | Why |
| --- | --- | --- |
| Library without per-major dependencies (Results, Domain, BuildingBlocks, IO, ...) | `net10.0` | The `net10.0` asset runs on .NET 11. A second asset adds build time and nothing for the consumer. Add `net11.0` only for a .NET 11 API, behind `#if NET11_0_OR_GREATER`. |
| Library that depends on ASP.NET Core, EF Core, Npgsql EF, or `Microsoft.Extensions.*` packages | `net10.0;net11.0` once its dependencies ship 11.x | ASP.NET Core 11 and EF Core 11 packages are `net11.0`-only, and EF Core 11 requires the .NET 11 runtime. Npgsql's EF provider 10.x requires EF Core `< 11.0.0`, so a `net10.0`-only library would force EF Core 10 on a .NET 11 application. Per-TFM dependency groups fix that. |
| Library blocked by a dependency | `net10.0` until it is ready | `Npgsql.EntityFrameworkCore.PostgreSQL` 11 is a release candidate (11.0.0-rc.3). Aspire 13.6.5 has no `net11.0` asset, and C# AppHosts require the .NET 10 SDK. Do not ship a dependency on a release candidate. |
| Analyzer, code fix, and source generator | `netstandard2.0` | Roslyn loads them in every SDK. Keep the Roslyn 5.6 compiler API floor described in [package development](package-development.md). |
| Test project | `net10.0;net11.0` | Each library runs on both runtimes. This is how 1 is proven for a `net10.0`-only library. Npgsql and OpenTelemetry test this way. |
| .NET tool (`PackAsTool`) | `net10.0` with `RollForward=Major` | See [Tools](#tools). |
| Aspire AppHost, test host, sample, benchmark | `net10.0` | Applications pick one TFM. |

## Tools

A tool is an application, and the SDK chooses the runtime for it. A `net10.0` tool whose runtimeconfig says
`rollForward: Major` runs on a machine that has only the .NET 11 runtime; `dotnet-ef` 11.0.0 ships this way, with a
single `tools/net10.0/any` folder. The repository's tools follow that pattern:

- `tools/Directory.Build.props` lists the tool TFMs once, in `SharedKernelToolTargetFrameworks` (now `net10.0`), and
  builds them through `TargetFrameworks`. It clears the `TargetFramework` that the root `Directory.Build.props` sets,
  because the SDK cross-targets only when `TargetFramework` is empty (`Sdks/Microsoft.NET.Sdk/Sdk/Sdk.targets`).
- It sets `RollForward` to `Major`, so the tool runs on .NET 10 when that is installed and on the next major runtime
  otherwise.
- `SharedKernel.Testing.CodeFixRunner` sets `LatestMajor`. It loads the SDK's MSBuild in process through
  `MSBuildLocator.RegisterDefaults()`, so it must run on the newest runtime to load an 11.0 SDK's MSBuild.

Add `net11.0` to `SharedKernelToolTargetFrameworks` only when a tool needs a .NET 11 API or must run on .NET 11 even
when .NET 10 is installed. The costs, *verified locally*:

- `dotnet run --project <tool>` fails with "Your project targets multiple frameworks" until each call passes
  `--framework`. With `net11.0` added to the tool list, `verify-target-frameworks` reports 43 such calls in the
  workflows and Markdown files, plus the SDK pin and the 10 tool lock files.
- The package holds both `tools/net10.0/any` and `tools/net11.0/any`, which doubles its size.
- Each tool's lock file gains a `net11.0` section.

## Mechanics

### SDK and runtimes

- Building `net11.0` needs the .NET 11 SDK; SDK 10.0.4xx stops at `net10.0` and reports NETSDK1045. SDK 11.0.100 builds
  `net10.0` too. Keep the exact pin with `rollForward: disable`: Microsoft recommends it with lock files, because a lock
  file does not isolate the graph from SDK changes.
- An SDK installs only its own runtime. Running `net10.0` tests under SDK 11 needs the .NET 10 runtime and ASP.NET Core
  10 runtime installed beside it. `actions/setup-dotnet` installs the `global-json-file` SDK plus every version listed in
  `dotnet-version`:

  ```yaml
  - uses: actions/setup-dotnet@<pinned sha>
    with:
      global-json-file: global.json
      dotnet-version: |
        10.0.x
  ```

  OpenTelemetry's CI uses this form. Confirm it against the README of the pinned action version, and add it to
  `.github/actions/setup-dotnet-tool/action.yml` and every job that runs tests or consumes candidates.
- SDK 10.0.404 (runtime 10.0.15, 2026-11-10) fixes 17 CVEs; the repository pins 10.0.403. Moving the pin to the
  current patch is a separate change from the move to SDK 11.

### Declaring target frameworks

Keep one property per project kind, as `tools/Directory.Build.props` does, instead of a list in each project file.
Where `TargetFrameworks` is set, clear the inherited `TargetFramework`:

```xml
<PropertyGroup>
    <SharedKernelLibraryTargetFrameworks Condition="'$(SharedKernelLibraryTargetFrameworks)' == ''">net10.0;net11.0</SharedKernelLibraryTargetFrameworks>
    <TargetFramework></TargetFramework>
    <TargetFrameworks>$(SharedKernelLibraryTargetFrameworks)</TargetFrameworks>
</PropertyGroup>
```

Without the empty `TargetFramework`, the project builds only `net10.0` and no error says so.

### Framework-specific code

- Use `#if NET11_0_OR_GREATER` for a .NET 11 API and keep the `net10.0` branch semantically identical. Each test run
  covers only its own branch, so the test project must target both TFMs.
- In MSBuild, compare frameworks with `$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net11.0'))`,
  not by parsing version strings. `src/Directory.Build.targets` already sets `IsAotCompatible` this way.
- Keep the public API the same on every TFM. A member that exists only on `net10.0` breaks a consumer that moves to
  `net11.0`.

### Public API files

When the public surface is the same on every TFM, the shared `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
stay as they are. When it differs, the analyzer's documented layout is a folder per TFM:

```xml
<ItemGroup>
    <AdditionalFiles Include="PublicAPI/$(TargetFramework)/PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI/$(TargetFramework)/PublicAPI.Unshipped.txt" />
</ItemGroup>
```

OpenTelemetry layers a shared pair with a per-TFM pair. That relies on the analyzer merging several files, which its
help page does not state; prefer the documented layout.

### Package validation

`EnablePackageValidation` runs three validators at pack time. The baseline validator needs a released stable version,
which is why [package development](package-development.md) keeps validation off. The compatible-framework validator
needs no baseline: it checks that code compiled against one asset in the package runs against the others. *Verified
locally*: a member compiled only into `lib/net10.0` fails the pack with `CP0002: Member '...' exists on
lib/net10.0/lib.dll but not on lib/net11.0/lib.dll`. Enable `EnablePackageValidation` for every multi-targeted
library; leave `PackageValidationBaselineVersion` unset until the first stable release.

### Central package versions

NuGet supports a different version per TFM under Central Package Management. Override inside a TFM-conditioned group:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">
    <PackageVersion Update="Microsoft.EntityFrameworkCore.Relational" Version="11.0.0" />
    <PackageVersion Update="Microsoft.AspNetCore.Authentication.JwtBearer" Version="11.0.0" />
</ItemGroup>
```

- Rule: a package that ships one line per .NET major (ASP.NET Core, EF Core, `Microsoft.Extensions.*`,
  `System.*`, `Microsoft.Data.Sqlite`, the `dotnet/extensions` packages, and providers that follow EF Core) uses the
  major of the TFM being built. A `net10.0` build gets 10.x and a `net11.0` build gets 11.x, even where an 11.x
  package also ships a `net10.0` asset. Support length (LTS or STS) plays no part.
- The default `PackageVersion` items hold the lowest supported TFM's versions; each later TFM overrides them in its own
  group. Transitive pins follow the same rule.
- The condition works only in the per-TFM (inner) evaluation, which is where package versions resolve.
- In .NET 11, nine `Microsoft.Extensions.*` abstraction packages moved into the shared framework. A `net11.0`-only
  project that references them gets NU1510; a project that targets `net10.0;net11.0` does not.
- `CentralPackageTransitivePinningEnabled` turns a pinned transitive package into a direct dependency of the packed
  library. Check that each TFM's dependency group in the packed `.nuspec` names the intended major.

### Lock files

A lock file has one `dependencies` section per TFM. Adding or removing a TFM fails `--locked-mode` with NU1004, so
regenerate the affected locks without locked mode (`dotnet restore SharedKernel.slnx --force-evaluate`) and commit them
with the TFM change. Then re-run the minimal retention rule in [package development](package-development.md#lock-files):
a new `net11.0` section adds `Id@Version` pairs, which can make a lock-exempt library need its lock file back.
`sharedkernel-version verify-lock-coverage` and `verify-consumer` already read every TFM section. SDK 11 adds NU1512 when
`RestoreForceEvaluate` and `RestoreLockedMode` are both set; CI sets locked mode, so do not force-evaluate in CI.

### Analysis level and language version

- With SDK 11, `AnalysisLevel=latest` selects the 11.0 rule set (*verified locally*: `_LatestAnalysisLevel` is `11.0` in
  SDK 11.0.100 and `10.0` in SDK 10.0.403). The solution built with no warnings under SDK 11.0.100 on 2026-11-15, so
  the move needs no pin today. If a later SDK adds findings under `AnalysisMode=All` and warnings as errors, pin
  `<AnalysisLevel>10.0</AnalysisLevel>` in the SDK change and fix the findings in their own change.
- `LangVersion=preview` on SDK 11 means the next C# preview, on every TFM. A language feature that needs .NET 11 types
  fails only in the `net10.0` build, which is why that build must run in CI.

### NativeAOT and trimming

The trim and AOT analyzers come from the SDK; the TFM only changes the annotations of the reference assemblies. SDK 11
changes NativeAOT output: a `lib` prefix on native library names on Unix, and non-blittable `UnmanagedCallersOnly`
signatures are rejected. Optimized AOT publish is opt-in (`UseOptimizedPublish` defaults to false, *verified locally*).
Packed-consumer NativeAOT checks must run per claimed TFM and platform.

### Tests

Under Microsoft.Testing.Platform, `dotnet test --solution` runs every TFM of each test project; `--framework` selects
one for local debugging. Expect test time to roughly double for the projects that target two frameworks, and measure
before and after.

## Repository changes

| Area | Now | Change |
| --- | --- | --- |
| `global.json` | SDK 10.0.403, `rollForward: disable` | 11.0.100 for the SDK move; 10.0.404 before it for the security fixes. |
| Root `Directory.Build.props` | `TargetFramework` `net10.0` | Stays the default; project kinds that multi-target clear it. |
| `tools/Directory.Build.props` | `SharedKernelToolTargetFrameworks` `net10.0`, `RollForward` `Major` | Add `net11.0` only when a tool needs it. |
| `tests/Directory.Build.props` | inherits `net10.0` | Add a test TFM property with `net10.0;net11.0`. |
| `Directory.Packages.props` | ASP.NET Core, EF Core, `Microsoft.Extensions.*` 10.0.14; OpenTelemetry 1.20 | `net11.0` version groups; OpenTelemetry 1.21 ships `net10.0` and `net11.0` assets. |
| Packed-consumer check | `tools/SharedKernel.Versioning.Tool/PackageConsumptionCommand.cs` writes `net10.0` consumers | Consume each candidate as `net10.0` and `net11.0`, including NativeAOT, on Linux and Windows. |
| `SharedKernelLocalFeed.WriteRestoreProject` | writes a `net10.0` restore project (public API) | Take the TFM as an argument in an API change. |
| Test helpers | `PackageConsumptionWorkspace` defaults to `net10.0`; Mediator consumer templates and tool package tests name `net10.0` | Parameterize where a test must cover both TFMs. |
| `sharedkernel-repo new` | seeds `net<SDK major>.0` | Unchanged: seeds follow the SDK major, so with SDK 11 a new repository starts on `net11.0`. |
| CI | installs only the `global.json` SDK | Add the 10.0 runtime where `net10.0` tests or consumers run. |
| Lock files | 111 `net10.0` sections, 1 `netstandard2.0` | Regenerate with each TFM change; re-run lock retention. |

## Order of changes

Each step is one reviewable change. Before each, edit the `targetFrameworks` policy in `.config/sharedkernel-repo.json`;
`sharedkernel-repo verify-target-frameworks` then lists the projects, lock files, SDK pin, and `dotnet run` calls to fix.

1. **SDK 10 (done in this change)**: this guide, the agent skill, `verify-target-frameworks`, and tools on a central
   TFM property with roll-forward.
2. **SDK patch**: move `global.json` to 10.0.404.
3. **SDK 11, same TFMs**: move `global.json` to 11.0.100, install the 10.0 runtime in CI, regenerate the lock files,
   and run full validation. A trial on 2026-11-15 needed nothing else: `--locked-mode` failed with NU1004 in 14 lock
   files because the SDK's implicit `Microsoft.NET.ILLink.Tasks` moved from 10.0.14 to 10.0.15, `--force-evaluate`
   rewrote only those entries, the solution built with no warnings, and the tests that ran passed. Expect the same
   lock change from SDK 10.0.404, which carries runtime 10.0.15.
4. **Tests on both runtimes**: test projects target `net10.0;net11.0`.
5. **Packed consumers on both TFMs**: the Versioning tool consumes candidates as `net10.0` and `net11.0`.
6. **Libraries with per-major dependencies**: multi-target, add `net11.0` version groups, enable package validation,
   and split public API files only where the surface differs. Npgsql EF and Aspire libraries wait for their 11.x
   releases.

To drop a TFM later, reverse the steps: change the policy, run the check, remove the TFM, its version groups, and its
`#if` branches, and regenerate the lock files.

## Decisions for the owner

| Decision | Options and cost | Recommendation |
| --- | --- | --- |
| Library TFMs | `net10.0` only (no `net11.0` asset for EF/ASP.NET libraries); multi-target all (double build time, no consumer benefit for dependency-free libraries); selective (two policies to maintain) | Selective, as in the policy table. |
| Tool TFMs | Roll-forward (one asset; runs on .NET 10 when installed); multi-target (two assets, `--framework` everywhere) | Roll-forward; multi-target a tool only when it needs .NET 11 APIs. |
| Npgsql EF and Aspire | Ship on release candidates; wait | Wait for stable releases. |
| `LangVersion=preview` | Keep (C# preview on `net10.0`, unsupported); use the TFM default | Decide before the SDK 11 move; the trial build passed with `preview`. |

## Sources

All accessed 2026-11-15.

- .NET 11 release notes and support dates: <https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md>,
  <https://dotnet.microsoft.com/platform/support/policy/dotnet-core>
- STS support extended to 24 months: <https://devblogs.microsoft.com/dotnet/dotnet-sts-releases-supported-for-24-months/>
- .NET 10.0.15 security release: <https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.15/10.0.15.md>
- Library targeting guidance: <https://learn.microsoft.com/dotnet/standard/library-guidance/cross-platform-targeting>
- Preprocessor symbols: <https://learn.microsoft.com/dotnet/standard/frameworks#preprocessor-symbols>
- SDK and TFM versions: <https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs>
- `global.json`: <https://learn.microsoft.com/dotnet/core/tools/global-json>
- `dotnet test` with Microsoft.Testing.Platform: <https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp>
- Central Package Management: <https://learn.microsoft.com/nuget/consume-packages/central-package-management>
- `Microsoft.Extensions.*` in the shared framework:
  <https://learn.microsoft.com/dotnet/core/compatibility/extensions/11/extensions-in-shared-framework>
- EF Core 11 requirements: <https://learn.microsoft.com/ef/core/what-is-new/ef-core-11.0/whatsnew>
- NU1004 and NU1512: <https://learn.microsoft.com/nuget/reference/errors-and-warnings/nu1004>,
  <https://learn.microsoft.com/nuget/reference/errors-and-warnings/nu1512>
- Public API analyzer help:
  <https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md>
- Package validation: <https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview>
- Analysis level: <https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#analysislevel>
- .NET 11 breaking changes: <https://learn.microsoft.com/dotnet/core/compatibility/11>
- `RollForward`: <https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#rollforward>
- Examples: OpenTelemetry .NET `build/Common.props` and `Directory.Packages.props`, dotnet/extensions
  `Directory.Build.props`, Aspire `eng/Versions.props`, and Npgsql `build.yml` on their `main` branches.
