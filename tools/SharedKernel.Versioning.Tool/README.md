# SharedKernel.Versioning.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain `<target>-alpha.<run>.<attempt>` previews; stable packages, tags, and releases require separate authorization. `calculate --version-kind prerelease` requires `--run-number`. Without a reachable `v*` tag the target is `0.1.0`. When the latest reachable `v*` tag is a prerelease, its version core is the fixed target and only the prerelease identifier advances; after a stable tag, the Conventional Commit impact selects the next target on each run until a prerelease tag fixes it. `--version-kind stable` releases the same target core. Commit impact is always reported. The generic `compute` command applies normal SemVer bumps.

`dotnet release` calculates SharedKernel versions from Conventional Commits, checks package APIs and
metadata, packs SharedKernel, creates and consumes hashed package candidates, and verifies a consumer repository
against a candidate. A consumer stages a pinned candidate
in its local feed with `dotnet ci stage-candidate` from the
[CI tool](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.CI.Tool/README.md).
The tool is a thin command line over the release engine in
[`PandaBear.SharedKernel.Versioning`](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Versioning/README.md),
which a host can call directly.

## Prerequisites

Git 2.24 or later, which `has-breaking-change-marker` needs for `--end-of-options`, and the .NET SDK pinned in
`global.json`. `--aot` also needs the platform
[NativeAOT toolchain](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/development.md#nativeaot-prerequisites).
`consume-candidate`, `consume-packages`, and `validate-candidate` run installed tool commands, whose shims find
.NET through `DOTNET_ROOT` or the default or registered install location. `verify-consumer` runs Git and the command it is given, found
in the absolute directories of `PATH` unless it is a full path.

## Install

Run from this repository without installing:

```text
dotnet run --project tools/SharedKernel.Versioning.Tool -c Release -- --help
```

Install a validated candidate as a local tool. With the SDK pinned in `global.json`, `dotnet tool install` rejects
`--add-source` under package source mapping, so add the candidate feed to `NuGet.Config`, map
`PandaBear.SharedKernel.*` to it, and keep every other package (`*`) mapped to the existing source:

```xml
<packageSources>
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  <add key="sharedkernel-candidates" value="<candidate-feed>" />
</packageSources>
<packageSourceMapping>
  <packageSource key="nuget.org">
    <package pattern="*" />
  </packageSource>
  <packageSource key="sharedkernel-candidates">
    <package pattern="PandaBear.SharedKernel.*" />
  </packageSource>
</packageSourceMapping>
```

`dotnet tool install` adds the tool to the local tool manifest and creates the manifest when none exists. Put `--`
before tool options, because `dotnet tool run` answers `--help` itself:

```text
dotnet tool install PandaBear.SharedKernel.Versioning.Tool --version <validated-candidate-version>
dotnet tool run release -- --help
```

## Usage

```text
dotnet release classify-commit <message>...
dotnet release compute --base <version> [--prerelease <label>] [--sha <sha>] < messages
dotnet release calculate [--version-kind prerelease|stable] [--run-number <number>] [--sha <sha>] [--github-output <path>] [--github-summary <path>]
dotnet release check-public-api-baselines
dotnet release validate-package-metadata
dotnet release verify-lock-coverage
dotnet release has-breaking-change-marker <git-range>
dotnet release check-api-compatibility [--version <version>] [--release-phase alpha|beta|rc|stable] [--baseline-version <version>] [--breaking-marker] [--output-root <path>]
dotnet release pack [--version <version>] [--assembly-version <version>] [--file-version <version>] [--informational-version <version>] [--output-root <path>] [--skip-restore-check]
dotnet release pack-dev --feed <path> [--version <version>] [--repo-root <path>] [<project>...]
dotnet release prepare-release --version <version> --package-dir <path> [--output-dir <path>] [--source-tag <tag>] [--release-impact <impact>] [--sha <sha>] < changes
dotnet release create-candidate [--version <version>] [--baseline-version <version>]
dotnet release compare-candidates --baseline <candidate.json> --candidate <candidate.json> --apicompat <path> --output <file>
dotnet release validate-candidate [--version <version>] [--baseline-version <version>] [--aot] [--framework <tfm>]...
dotnet release consume-candidate (--candidate <path> | --candidate-directory <path>) [--output <path>] [--aot] [--framework <tfm>]...
dotnet release consume-packages --feed <path> --version <version> [--aot]
dotnet release merge-evidence --candidate <path> --evidence <path>... [--output <path>]
dotnet release verify-consumer --candidate <file> [--solution <file>] [--output <file>] -- <command>...
```

Commands that operate on a checkout take `--repo-root <path>`, which defaults to the current directory.
`dotnet release <command> --help` describes each option. A command line that starts with
`[diagram]` prints how it parses without running the command, and exits 1 when it does not parse.

### Versions

```text
dotnet release classify-commit "feat(versioning): emit JSON output"
git log --format=%B%x00 v0.1.0..HEAD | dotnet release compute --base 0.1.0 --prerelease alpha.1 --sha abc123
dotnet release calculate --version-kind stable --sha abc123
```

`classify-commit` prints `major`, `minor`, `patch`, or `none`. `compute` reads null- or newline-separated commit
messages from standard input; use null separators for raw `git log` output so multiline bodies stay together.
Messages that are not Conventional Commits, such as merge commits, are ignored. `calculate` reads the
messages since the latest `v*` tag, or uses base `0.1.0` without one, and labels prerelease versions `alpha.<run-number>`
when `--run-number` is set.

Both commands print version JSON with `semVer`, `releaseImpact`, `packageVersion`, `assemblyVersion`, `fileVersion`,
and `informationalVersion`. `--github-output` appends `base_version`, `source_tag`, `version_json`, `sem_ver`,
`release_impact`, `package_version`, `assembly_version`, `file_version`, and `informational_version`;
`--github-summary` appends a Markdown summary. Release workflows use `package_version` as the package identity.

### Checks

```text
dotnet release check-public-api-baselines
dotnet release validate-package-metadata
dotnet release verify-lock-coverage
dotnet release has-breaking-change-marker origin/main..HEAD
dotnet release check-api-compatibility --version 0.2.0-beta.1 --release-phase beta --breaking-marker --baseline-version 0.1.0
```

`verify-lock-coverage` is the lock coverage drift guard. The dependency snapshot reads committed `packages.lock.json` files only,
so a source project that keeps none (`lockExemptProjects` in `.config/sharedkernel-repo.json`) must resolve only package versions
that some committed lock file resolves. The command reads the restored `obj/project.assets.json` of each such project and exits 2,
naming `project: package@version` and the remedy, for a package version that no committed lock resolves. A lock-free project with
no restored assets also exits 2 (run `dotnet restore SharedKernel.slnx --locked-mode` first), so it never passes vacuously. A
repository whose source projects all keep a lock file passes with zero projects checked. It writes nothing and needs no package
directory; the validation workflow runs it in `repository-quality`, which restores the whole solution.

`has-breaking-change-marker` succeeds when a commit in the range has a `!` header or a `BREAKING CHANGE:` or
`BREAKING-CHANGE:` footer, and exits 3 when no commit has one. Arguments that start with `@` name response
files, so write `HEAD@{u}..HEAD` instead of `@{u}..HEAD`.

`check-api-compatibility` writes `<output-root>/<version>/api-compatibility-report.md` and packs into
`<output-root>/packages/<version>`. With `--baseline-version`, packing runs .NET package validation against that
version; without it, the packages must restore from the local feed. Failures fail `rc` and `stable`, fail `beta`
unless `--breaking-marker` is set, and are only reported for `alpha`, the default. Without `--version`, the
version is `0.1.0-alpha.local.<UTC timestamp>`.

### Packages and releases

```text
dotnet release pack --version 0.2.0-alpha.1
dotnet release prepare-release --version 0.2.0 --package-dir artifacts/packages/local/0.2.0 < changes.txt
```

`pack` packs the `src/SharedKernel` projects, the consumer-support test projects, and the packaged
tools into `<output-root>/<version>`, which defaults to `artifacts/packages/local/<version>`. It refuses an existing
version and, unless `--skip-restore-check` is set, restores the packages and installs the tools from that folder. That
restore treats warnings as errors and audits like the sample projects of `consume-candidate`, and the packages that the
tools carry are audited as `consume-candidate` does before the tools install (see [Candidates](#candidates)).
Without `--version`, the version is `0.1.0-alpha.local.<UTC timestamp>`.

`prepare-release` reads change lines from standard input and writes `release-notes.md`, `CHANGELOG.md`,
`release-manifest.json`, `third-party-attributions.json`, `third-party-notices.md`, and `sbom.spdx.json` to
`--output-dir`, which defaults to `artifacts/release-prep`. Set `SOURCE_DATE_EPOCH` for a reproducible SBOM timestamp.
The package inventory reads the `packages.lock.json` next to each project under `src`; a project without one is read from its
restored `obj/project.assets.json`, so restore the solution first. The command fails and lists the project and package when such
a project resolves a package version that no committed lock file resolves, because the dependency snapshot would omit it.

### Local development packs

```text
dotnet release pack-dev --feed ../app/.local/sharedkernel-feed SharedKernel.Results
```

`pack-dev` is the fast loop for a consumer that develops against uncommitted library changes. It packs the named projects
(`SharedKernel.Results`, not the package id) from the working tree into `--feed` as `--version` (default `0.1.0-dev.local`),
packs their project-reference dependencies only when the feed does not hold them yet (a kept dependency whose `*.cs`, `*.csproj`, `*.props` or `*.targets` files, outside `bin/` and `obj/`, changed after its package was written is reported as `Kept   <id> (stale: sources changed after it was packed; name it to repack)`; it is warned about, never repacked automatically), and removes every repacked package
from the NuGet global packages folder, which caches by id and version and would otherwise keep serving the previous build.
The folder is a non-empty `NUGET_PACKAGES`, else the one `dotnet nuget locals global-packages --list` reports from the current
directory (honoring `globalPackagesFolder` in `NuGet.Config`), else `~/.nuget/packages`; each line says whether a cached copy
was evicted. `--repo-root` may be relative. Without project names it repacks every SharedKernel project. It runs no API
checks and records no manifest, so its output is never a candidate; use `create-candidate` for that.

### Candidates

```text
dotnet release validate-candidate --aot
dotnet release consume-candidate --candidate-directory artifacts/candidate --aot
dotnet release consume-candidate --candidate-directory artifacts/candidate --framework net10.0 --framework net11.0
dotnet release consume-packages --feed artifacts/api-compat/packages/0.2.0-alpha.1 --version 0.2.0-alpha.1
```

`create-candidate` requires a clean committed checkout. It runs `check-api-compatibility` in the `beta` phase and
writes `candidate.json` beside the packages in `artifacts/api-compat/packages/<version>`, recording the library
revision and SHA-256 package hashes. Consumer, platform, and stable-promotion evidence stay `pending`. Without
`--version`, the version is `<base>-alpha.local.t<yyyyMMddHHmmss UTC>.g<7-character revision>`, where `<base>` is the
version core of `ComputedSemVer` in `Directory.Build.props`. It stays short because NuGet rejects a package whose id,
version and file path together exceed its path limit (NU5123), and the longest package ids already use most of it. `--baseline-version` requires `PandaBear.SharedKernel.*` mapped only
to an existing local feed in `NuGet.Config`.

`consume-candidate` verifies the hashes and requires the manifest to list the exact file name of every package in its
directory, and every package to have the candidate version. It first audits the packages that each tool package
carries. `dotnet tool install` restores no project, so NuGet Audit never sees what it installs, and NuGet refuses a
tool package as a project's package reference (`NU1212`). Each `tools/<tfm>/any` folder that holds a
`DotnetToolSettings.xml` must therefore hold the dependency manifest (`*.deps.json`) that the build writes beside the
tool; for each framework whose manifest lists a library of type `package`, the check restores a scratch project that
references each of them at its exact version. A failed restore fails the check, and the error names the tool package
and the framework. It then installs each tool package into a scratch tool path, from a generated NuGet configuration
whose only source is that directory, and runs each command that the package's `tools/<tfm>/any/DotnetToolSettings.xml`
declares with `--help` from an empty scratch directory. It deletes the scratch directory afterwards. The check fails
when a package does not install or a command exits with a non-zero code, and the error names the package and any
failing command. It then restores, builds, and runs the samples against the packages with isolated caches, and records
`passed` or `failed` for this platform's package consumption, and its NativeAOT run with `--aot`, in the manifest or in
`--output`. It replaces that file through a temporary file in the same directory, so an interrupted write never
truncates it, and Ctrl+C or SIGTERM leaves the file unchanged.

The sample projects and the tool audit projects treat warnings as errors and audit every direct and transitive package
at NuGet's default level, `low`, so an advisory of any severity fails consumption. Their generated `NuGet.config` names
nuget.org as the only [audit source](https://learn.microsoft.com/nuget/concepts/auditing-packages#audit-sources), so
an audit that gets no vulnerability data fails as well, with `NU1900` (nuget.org did not answer) or `NU1905` (it
provided no vulnerability data). Without an audit source NuGet audits against the package sources and suppresses
`NU1905`, so a package source without vulnerability data, such as the candidate folder, goes unreported.
`--framework <tfm>` (`consume-candidate`, `consume-packages`, `validate-candidate`) names a target framework to build the
consumers for, such as `net11.0`; repeat it for several. Without it, the tool consumes the framework(s) set in the repository root's `Directory.Build.props` (`TargetFrameworks`, else `TargetFramework`, with `$(Name)` references resolved in that file) and fails naming the file when that setting is missing or conditional. A value must be `net` plus
`<major>.<minor>` in lowercase, listed once. The restore, locked restore, build, run, and optional NativeAOT publish run once per
framework, each in its own workspace and package cache, with the central package versions that a project of that framework
uses, and a failure names the framework. The manifest records
the consumed frameworks beside the result, as `<platform>PackageConsumptionFrameworks` and, with `--aot`,
`<platform>NativeAotFrameworks`; `merge-evidence` copies them with the result. The SDK must be able to build each framework.
`--candidate-directory` must contain exactly one `candidate.json`, searched recursively. `consume-packages` runs
the same checks against a feed without a manifest.
`validate-candidate` runs `create-candidate` and then `consume-candidate` on the manifest it wrote.

`merge-evidence` copies the `passed` platform results (`linuxPackageConsumption`, `windowsPackageConsumption`,
`linuxNativeAot`, `windowsNativeAot`) from one or more evidence files into a candidate manifest, normally a
consumer's committed pin. Each evidence file must carry the candidate's `libraryRevision`, `libraryDirty`,
`packageVersion` and `packageSha256` exactly. A `failed` result or a mismatch exits 2 and writes nothing. Other
fields stay as they are, and it never lowers a recorded result.

```text
dotnet release verify-consumer --candidate artifacts/candidate/candidate.json -- dotnet test --solution App.slnx -c Release
```

`verify-consumer` runs in a consumer repository. Relative paths resolve from the current directory. It verifies the
candidate's package hashes, then:

1. records the consumer's `HEAD` and whether `git status --porcelain` is empty; with uncommitted consumer changes or
   a candidate whose `libraryDirty` is not `false`, the result is `pending` and nothing runs;
2. clones the repository with `git clone --no-checkout --shared` into a scratch directory under the system
   temporary directory and checks out the recorded revision detached;
3. in the clone, requires the candidate version on all `PandaBear.SharedKernel.*` central package versions, entries in
   the selected projects' `packages.lock.json` files, and local tools, comparing IDs and versions case-insensitively.
   The selected projects come from `--solution` (default: the only `.slnx` in the repository root). A project can omit
   its lock file when it is listed under `lockExemptProjects` in `.config/sharedkernel-repo.json` and sets
   `RestorePackagesWithLockFile` to `false` unconditionally in its project file. Explicit SharedKernel package versions
   and overrides in exempt project files must also match the candidate. A project or lock entry that references
   SharedKernel source fails the check, as does a consumer that references no candidate package;
4. requires the clone's `NuGet.Config` to clear `packageSources` and `fallbackPackageFolders` and to map
   `PandaBear.SharedKernel.*` to exactly one source, a repository-relative folder that holds no package, and copies
   the candidate's packages there;
5. runs the command after `--`, with the clone as its working directory and `NUGET_PACKAGES` and
   `NUGET_HTTP_CACHE_PATH` in the scratch directory; the command's output streams to standard error.

Use a locked restore and the consumer's [lock coverage checks](../../docs/package-development.md#lock-files) in
the verification command to confirm that retained locks cover the resolved packages of every exempt project.

`--output` (default `TestResults/consumer-compatibility.json` in `--repo-root`) receives the candidate manifest with
`consumerRevision`, `consumerDirty`, `consumerCompatibility` (`passed`, `failed`, or `pending`), and, unless it
passed, a `reason` of at most 4,096 characters. It is written through a temporary file, and it can never be the
candidate manifest. The command exits 0 only when the result is `passed`. Ctrl+C or SIGTERM stops the command's
process tree and writes no evidence, so a canceled run never records a result. The scratch directory is deleted in
every case.

### Compare archived candidates

Stage each exact candidate manifest beside its archives in a separate directory. Specify the baseline explicitly and
use a new output file. The comparison verifies each file named by both manifests and rejects unlisted archives, then
runs Microsoft ApiCompat package validation against the same verified bytes. It pairs packages by NuGet ID. Install
the tested [Microsoft ApiCompat release](https://www.nuget.org/packages/Microsoft.DotNet.ApiCompat.Tool/11.0.100)
into a dedicated local tool directory.

```sh
dotnet tool install Microsoft.DotNet.ApiCompat.Tool --version 11.0.100 --tool-path .local/api-compat
dotnet release compare-candidates --baseline prior/candidate.json --candidate current/candidate.json --apicompat .local/api-compat/apicompat --output current/api-comparison.json
```

On Windows, pass the installed `apicompat.exe` path. Exit 0 means the comparison completed with no differences.
Exit 3 means it completed and found differences, including a removed package ID. Both write the JSON report.
Missing or invalid evidence and failed tool execution exit 2 without producing a report. The report carries both
manifest hashes, revisions, versions and package hashes, the ApiCompat version, and each package's diagnostics and
status. A new package is listed as added. A common package without a baseline assembly under `lib`, `ref`, or
runtime-specific `lib` is listed separately from checked packages. Verify tool commands, generator output, and consumer
builds through their existing validation lanes.

## Output and exit codes

Results such as versions, JSON, and report or package paths go to standard output. Errors go to standard error, as
does the output of the `dotnet` processes, tool commands, and samples that `consume-candidate`, `consume-packages`,
and `validate-candidate` run, and of the command that `verify-consumer` runs.

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | Invalid command line, cancellation, or an unexpected internal error. |
| `2` | The command failed: invalid input, a failed check, or a failed `git`, `dotnet`, or tool process. |
| `3` | `has-breaking-change-marker` found no marker, or `compare-candidates` reported API differences. |
| `130`, `143` | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within two seconds. |

Ctrl+C or SIGTERM stops the running `git`, `dotnet`, tool, or sample process tree. A process that fails after the tool
has observed Ctrl+C or SIGTERM counts as the cancellation.

## Security

- .NET tools run in full trust. Install only from trusted manifests and package sources.
- No option accepts a secret.
- `has-breaking-change-marker` rejects an empty range or one that starts with `-`, and passes the range to Git after
  `--end-of-options`, so Git cannot read it as an option.
- Packing and consumption restore third-party packages from nuget.org. Generated NuGet configurations map
  `PandaBear.SharedKernel.*` only to the local feed, so internal packages never resolve from nuget.org, and name
  nuget.org as their only audit source, so an audit without vulnerability data fails instead of passing empty.
- Consumption installs tool packages only from the candidate directory, and reads their tool settings without
  document type definitions, so no entity expands and no external resource is read. Before a tool installs or runs,
  NuGet Audit checks the packages that its dependency manifest lists.
- Candidate commands verify SHA-256 package hashes before consuming packages.
- `verify-consumer` runs its command on a clone of the committed revision, never in the working tree, with NuGet caches
  of its own, and verifies the staged package copies against their hashes.

## Packaging and AOT

`PackAsTool=true`, `ToolCommandName=release`, and `PackageReadmeFile=README.md` define the tool package.
The tool is analyzer-clean with `IsAotCompatible=true` and ships as a framework-dependent tool, not a RID-specific
native binary. Repository check for a native Linux x64 binary:

```text
dotnet publish tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj -c Release -r linux-x64 -p:PublishAot=true -p:IsRoslynComponent=true -o artifacts/sharedkernel-version-aot-check
```

`IsRoslynComponent=true` skips the repository analyzer project references so the NativeAOT publish property does not
apply to analyzer projects.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning.Tool`. C# namespace: `SharedKernel.Versioning.Tool`. Assembly/project identity: `SharedKernel.Versioning.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
