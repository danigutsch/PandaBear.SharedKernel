# .NET versions and target frameworks

Every project of this repository builds one .NET version, set in one place. The repository tools support repositories
that build several. Facts were checked on 2026-11-15 against the sources at the end; items marked *verified locally*
were reproduced with SDK 11.0.100 on Linux.

The [target framework agent skill](../.agents/skills/dotnet-multi-targeting/SKILL.md) applies this guide to a change.

## The setting

The root `Directory.Build.props` names the framework once:

```xml
<SharedKernelTargetFramework>net11.0</SharedKernelTargetFramework>
<TargetFramework>$(SharedKernelTargetFramework)</TargetFramework>
```

Libraries, tests, samples, and tools inherit it. Analyzers, code fixes, and generators set `netstandard2.0`, which is not
a .NET version; they compile C# 15 like the rest of the repository (root `Directory.Build.targets`). Tools keep
`RollForward` `Major` (`LatestMajor` for the code-fix runner, which loads the SDK's MSBuild), so they also run where only
a later runtime is installed.

The setting is the only place that names a .NET framework. `sharedkernel-repo verify-target-frameworks` enforces it:

- The `targetFrameworks` policy in `.config/sharedkernel-repo.json` names `"frameworkProperties": "Directory.Build.props"`, and its rules say
  `"frameworks": ["$(SharedKernelTargetFramework)"]` instead of a moniker.
- It reports a .NET framework name anywhere else in a tracked project, props, targets, nuspec, workflow, action, or
  script file, or in the policy file.
- `sharedkernel-version` consumes packed candidates as the repository's framework unless `--framework` is given.
- CI installs the SDK from `global.json`. The shared `setup-dotnet-tool` action adds the runtime of the setting's
  framework only when it is older than the SDK.
- The generator package's `lib/` folder and dependency group come from the setting, and tests that describe this
  repository take the framework they were built for.

## Moving to the next .NET version

1. Pin the new SDK in `global.json`.
2. Change `SharedKernelTargetFramework`.
3. Move the packages that ship one line per .NET version to the new line, except those blocked by a dependency (see
   Packages).
4. Run `dotnet restore SharedKernel.slnx --force-evaluate` to regenerate the lock files, then build, test, and run
   `sharedkernel-version validate-candidate --aot`.
5. Fix what the new analyzers and runtime report (see What .NET 11 changed).
6. Mark the change as breaking (`!` in the title and a `BREAKING CHANGE:` footer). Applications on the old version
   cannot take the new packages.

The `dotnet-preview` workflow (weekly and on demand) does steps 1 and 2 against the next major's preview SDK, without
lock files. It finds that version's breaking changes during its previews rather than at release. Without a preview it
reports that and succeeds; it is not required for merging.

## Releases

| | .NET 10 | .NET 11 | .NET 12 |
| --- | --- | --- | --- |
| Support | LTS, released 2025-11-11 | STS, released 2026-11-10 | LTS, due November 2027 |
| End of support | 2028-11-14 | 2028-11-14 | |
| Default C# | 14 | 15 | |

STS releases are supported for 24 months since .NET 9, so .NET 10 and .NET 11 end on the same day. Building only
`net11.0` shortens nothing; applications still on .NET 10 cannot take package versions released after the move.

## Packages

A package keeps an older major version when that version works on the repository's framework and nothing requires the
newer one:

- ASP.NET Core, `Microsoft.Extensions.*`, and `System.Security.*` use 11.0.0, and
  `Microsoft.OpenApi` 3.11.0, which `Microsoft.AspNetCore.OpenApi` 11 requires.
- The EF family stays on 10.x until `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0 is stable: every package whose 11.x
  requires EF Core 11, and `Microsoft.Data.Sqlite.Core`, which the EF Core Sqlite provider is built against.
  - Npgsql's 10.0.3 provider requires EF Core `[10.0.4, 11.0.0)`, and `11.0.0-rc.3` requires exactly EF Core
    `11.0.0-rc.3.26522.148` (nuspecs checked 2026-11-15).
  - EF Core 10 targets .NET 10, works from `net11.0`, and is supported until 2028-11-10.
  - EF Core 11 is not binary compatible with code built against EF Core 10, so applications move to EF Core 11 together
    with these packages.
- `Microsoft.NETCore.App` 11 contains the `Microsoft.Extensions.*.Abstractions` packages for caching, configuration,
  dependency injection, diagnostics, file providers, hosting, and logging, plus `Options` and `Primitives`. No project
  references them, so they are not dependencies of the packages.
- Analyzers and generators build against Roslyn 5.12.0, the compiler of SDK 11.0.100, and the code-fix runner against
  MSBuild 18.12.2.

## Repositories that build several frameworks

The repository tools keep their multi-framework support, with tests, for consuming repositories and for this one should
it build two versions again:

| Tool | Multi-framework support |
| --- | --- |
| `sharedkernel-repo verify-target-frameworks` | Rules list several monikers or name a property with a `;` list (unique, ascending); checks lock files per framework, a `TargetFramework` that overrides `TargetFrameworks`, `RollForward`, the SDK, and `dotnet run --project` calls that need `--framework` |
| `sharedkernel-version` | A repeated `--framework` consumes the packages once per framework, each with the central package versions of that framework, and records `<platform>PackageConsumptionFrameworks` and `<platform>NativeAotFrameworks` |
| `sharedkernel-ci install-playwright` | Uses the installer of the first framework folder of a multi-framework test project |
| Governed-literal guard | Checks each framework's compiler-input manifest separately |

A repository that builds several frameworks needs these patterns (*verified locally* in PR #396, which built
`net10.0;net11.0`):

- **Projects:** a project that sets `TargetFrameworks` must clear an inherited `TargetFramework`
  (`<TargetFramework></TargetFramework>`), because the SDK cross-targets only when `TargetFramework` is empty. The
  Aspire AppHost SDK 13.6.5 fails in a multi-framework build (MSB4036 for `PickBestRid`), so an AppHost keeps one
  framework.
- **Package versions:** the items in `Directory.Packages.props` hold the oldest framework's versions. Each newer
  framework gets
  `<ItemGroup Condition="'$(TargetFramework)' == '<tfm>'">` with `PackageVersion Update=` items for the packages that
  ship one line per .NET version. `sharedkernel-version` reads only that condition shape.
- **Package validation:** set `EnablePackageValidation` for packages with several frameworks; without a baseline it runs
  the compatible-framework check (CP0002).
- **Lock files:** one section per framework; changing frameworks needs a restore without `--locked-mode` (NU1004).
- **Code:** `#if NET11_0_OR_GREATER` style branches only where an API or a dependency differs, and the older framework's
  build must run in CI.
- **CI:** install the older runtimes beside the SDK; `dotnet test --solution` runs every framework.
- **Tools:** prefer one framework with `RollForward`; a multi-framework tool needs `dotnet run --framework`.

## What .NET 11 changed for this repository

- `string.StartsWith`, `EndsWith`, `IndexOf`, and `LastIndexOf` gained `(char, StringComparison)` overloads, so CA1307
  reports the `char` overloads; the code passes `StringComparison.Ordinal`, which is what the `char` overloads did.
- CA2016 reports `TextWriter.WriteAsync` and `WriteLineAsync` calls that do not pass the cancellation token; the code
  passes `CancellationToken.None` through the `ReadOnlyMemory<char>` overloads, which keeps the old behavior: a write that
  reports a cancellation must not throw. CA2263 prefers `JsonSerializerOptions.GetTypeInfo<T>()`.
- `System.Net.Http.Json` is an implicit using of `Microsoft.NET.Sdk` projects for `net11.0`, so explicit `using`
  directives for it are unnecessary.
- `System.IO.ReadOnlyMemoryStream` is new and public, with the same constructor and members as
  `PandaBear.IO.ReadOnlyMemoryStream` (*verified locally* by reflection and against the `release/11.0` source). It also
  caches the completed read task, throws for `WriteAsync` and `WriteByte` directly, and rejects positions beyond
  `int.MaxValue`. Ours is removed: an unqualified name would be ambiguous (CS0104) for code that imports `PandaBear.IO`,
  because `System.IO` is an implicit using. It was the only public type of this repository that .NET 11 or ASP.NET
  Core 11 adds under the same name (*verified locally* by diffing the 10.0.15 and 11.0.0 reference packs).
- EF Core 11 lists `IIndex.Properties` as `IPropertyBase`; `UniqueRuleResolver` handles both that and EF Core 10's
  `IProperty`.
- ASP.NET Core 11 makes `EditContext.Validate()` obsolete in favor of `ValidateAsync`, which must run on the renderer's
  dispatcher, as Blazor event handlers do.
- `Microsoft.AspNetCore.OpenApi` 11 requires `Microsoft.OpenApi` 3.x, whose content maps hold `IOpenApiMediaType`.
- `Convert.ToDecimal(double)` and `new decimal(double)` are exact: `1.005d` converts to `1.0049999999999998934…`
  instead of `1.005`. xUnit converts `[InlineData]` doubles for `decimal` parameters this way, so decimal test data is
  written as strings. No library code converts a double to a decimal.
- `Math.Round(double, digits)` accepts more than 15 digits instead of throwing `ArgumentOutOfRangeException`.
- `Convert.TryFromBase64String` rejects padded text whose last character has nonzero unused bits (`QR==`), which
  .NET 10 decoded like `QQ==`. The vault manager clears those bits before it retries. Verified locally; no breaking-change
  notice was found.
- `UseExceptionHandler` keeps the 400 status of a `BadHttpRequestException`; earlier runtimes answer 500.

## Sources

All accessed 2026-11-15.

- .NET 11 release notes and support dates: <https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md>,
  <https://dotnet.microsoft.com/platform/support/policy/dotnet-core>
- STS support extended to 24 months: <https://devblogs.microsoft.com/dotnet/dotnet-sts-releases-supported-for-24-months/>
- EF Core releases and support: <https://learn.microsoft.com/ef/core/what-is-new/>
- Library targeting guidance: <https://learn.microsoft.com/dotnet/standard/library-guidance/cross-platform-targeting>
- Preprocessor symbols: <https://learn.microsoft.com/dotnet/standard/frameworks#preprocessor-symbols>
- SDK and TFM versions: <https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs>
- `global.json`: <https://learn.microsoft.com/dotnet/core/tools/global-json>
- `dotnet test` with Microsoft.Testing.Platform: <https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp>
- Central Package Management: <https://learn.microsoft.com/nuget/consume-packages/central-package-management>
- `Microsoft.Extensions.*` in the shared framework:
  <https://learn.microsoft.com/dotnet/core/compatibility/extensions/11/extensions-in-shared-framework>
- NU1004: <https://learn.microsoft.com/nuget/reference/errors-and-warnings/nu1004>
- Package validation: <https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview>
- .NET 11 breaking changes: <https://learn.microsoft.com/dotnet/core/compatibility/11>
- `RollForward`: <https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#rollforward>
