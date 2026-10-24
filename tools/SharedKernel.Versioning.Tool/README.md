# SharedKernel.Versioning.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain `<target>-alpha.<run>.<attempt>` previews; stable packages, tags, and releases require separate authorization. `calculate-release --version-kind prerelease` requires `--run-number`. Without a reachable `v*` tag the target is `0.1.0`. When the latest reachable `v*` tag is a prerelease, its version core is the fixed target and only the prerelease identifier advances; after a stable tag, the Conventional Commit impact selects the next target on each run until a prerelease tag fixes it. `--version-kind stable` releases the same target core. Commit impact is always reported. The generic `compute` command applies normal SemVer bumps.

`sharedkernel-version` calculates SharedKernel versions from Conventional Commits, checks package APIs and
metadata, packs SharedKernel, and creates and consumes hashed package candidates. A consumer stages a pinned candidate
in its local feed with `sharedkernel-ci stage-candidate` from the
[CI tool](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.CI.Tool/README.md).

## Prerequisites

Git 2.24 or later, which `has-breaking-change-marker` needs for `--end-of-options`, and the .NET SDK pinned in
`global.json`. `--aot` also needs the platform
[NativeAOT toolchain](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/development.md#nativeaot-prerequisites).

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
dotnet tool run sharedkernel-version -- --help
```

## Usage

```text
sharedkernel-version classify-commit <message>...
sharedkernel-version compute --base <version> [--prerelease <label>] [--sha <sha>] < messages
sharedkernel-version calculate-release [--version-kind prerelease|stable] [--run-number <number>] [--sha <sha>] [--github-output <path>] [--github-summary <path>]
sharedkernel-version check-public-api-baselines
sharedkernel-version validate-package-metadata
sharedkernel-version has-breaking-change-marker <git-range>
sharedkernel-version check-api-compatibility [--version <version>] [--release-phase alpha|beta|rc|stable] [--baseline-version <version>] [--breaking-marker] [--output-root <path>]
sharedkernel-version pack-sharedkernel [--version <version>] [--assembly-version <version>] [--file-version <version>] [--informational-version <version>] [--output-root <path>] [--skip-restore-check]
sharedkernel-version prepare-release --version <version> --package-dir <path> [--output-dir <path>] [--source-tag <tag>] [--release-impact <impact>] [--sha <sha>] < changes
sharedkernel-version create-candidate [--version <version>] [--baseline-version <version>]
sharedkernel-version validate-candidate [--version <version>] [--baseline-version <version>] [--aot]
sharedkernel-version consume-candidate (--candidate <path> | --candidate-directory <path>) [--output <path>] [--aot]
sharedkernel-version consume-packages --feed <path> --version <version> [--aot]
```

Every command except `classify-commit` and `compute` accepts `--repo-root <path>`, which defaults to the current
directory. `sharedkernel-version <command> --help` describes each option.

### Versions

```text
sharedkernel-version classify-commit "feat(versioning): emit JSON output"
git log --format=%B%x00 v0.1.0..HEAD | sharedkernel-version compute --base 0.1.0 --prerelease alpha.1 --sha abc123
sharedkernel-version calculate-release --version-kind stable --sha abc123
```

`classify-commit` prints `major`, `minor`, `patch`, or `none`. `compute` reads null- or newline-separated commit
messages from standard input; use null separators for raw `git log` output so multiline bodies stay together.
Messages that are not Conventional Commits, such as merge commits, are ignored. `calculate-release` reads the
messages since the latest `v*` tag, or uses base `0.1.0` without one, and labels prerelease versions `alpha.<run-number>`
when `--run-number` is set.

Both commands print version JSON with `semVer`, `releaseImpact`, `packageVersion`, `assemblyVersion`, `fileVersion`,
and `informationalVersion`. `--github-output` appends `base_version`, `source_tag`, `version_json`, `sem_ver`,
`release_impact`, `package_version`, `assembly_version`, `file_version`, and `informational_version`;
`--github-summary` appends a Markdown summary. Release workflows use `package_version` as the package identity.

### Checks

```text
sharedkernel-version check-public-api-baselines
sharedkernel-version validate-package-metadata
sharedkernel-version has-breaking-change-marker origin/main..HEAD
sharedkernel-version check-api-compatibility --version 0.2.0-beta.1 --release-phase beta --breaking-marker --baseline-version 0.1.0
```

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
sharedkernel-version pack-sharedkernel --version 0.2.0-alpha.1
sharedkernel-version prepare-release --version 0.2.0 --package-dir artifacts/packages/local/0.2.0 < changes.txt
```

`pack-sharedkernel` packs the `src/SharedKernel` projects, the consumer-support test projects, and the packaged
tools into `<output-root>/<version>`, which defaults to `artifacts/packages/local/<version>`. It refuses an existing
version and, unless `--skip-restore-check` is set, restores the packages and installs the tools from that folder.
Without `--version`, the version is `0.1.0-alpha.local.<UTC timestamp>`.

`prepare-release` reads change lines from standard input and writes `release-notes.md`, `CHANGELOG.md`,
`release-manifest.json`, `third-party-attributions.json`, `third-party-notices.md`, and `sbom.spdx.json` to
`--output-dir`, which defaults to `artifacts/release-prep`. Set `SOURCE_DATE_EPOCH` for a reproducible SBOM timestamp.

### Candidates

```text
sharedkernel-version validate-candidate --aot
sharedkernel-version consume-candidate --candidate-directory artifacts/candidate --aot
sharedkernel-version consume-packages --feed artifacts/api-compat/packages/0.2.0-alpha.1 --version 0.2.0-alpha.1
```

`create-candidate` requires a clean committed checkout. It runs `check-api-compatibility` in the `beta` phase and
writes `candidate.json` beside the packages in `artifacts/api-compat/packages/<version>`, recording the library
revision and SHA-256 package hashes. Consumer, platform, and stable-promotion evidence stay `pending`. Without
`--version`, the version is `<base>-alpha.local.t<UTC timestamp>.g<revision>`, where `<base>` is the version core
of `ComputedSemVer` in `Directory.Build.props`. `--baseline-version` requires `PandaBear.SharedKernel.*` mapped only
to an existing local feed in `NuGet.Config`.

`consume-candidate` verifies the hashes, restores, builds, and runs the samples against those packages with
isolated caches, and records `passed` or `failed` for this platform's package consumption, and its NativeAOT run
with `--aot`, in the manifest or in `--output`. It replaces that file through a temporary file in the same
directory, so an interrupted write never truncates it, and Ctrl+C or SIGTERM leaves the file unchanged.
`--candidate-directory` must contain exactly one `candidate.json`, searched recursively. `consume-packages` runs
the same checks against a feed without a manifest.
`validate-candidate` runs `create-candidate` and then `consume-candidate` on the manifest it wrote.

## Output and exit codes

Results such as versions, JSON, and report or package paths go to standard output. Errors go to standard error, as
does the output of the `dotnet` processes and samples that `consume-candidate`, `consume-packages`, and
`validate-candidate` run.

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | Invalid command line, cancellation, or an unexpected internal error. |
| `2` | The command failed: invalid input, a failed check, or a failed `git` or `dotnet` process. |
| `3` | `has-breaking-change-marker` found no breaking-change marker in the range. |
| `130`, `143` | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within two seconds. |

Ctrl+C or SIGTERM stops the running `git`, `dotnet`, or sample process tree. A process that fails after the tool has
observed Ctrl+C or SIGTERM counts as the cancellation.

## Security

- .NET tools run in full trust. Install only from trusted manifests and package sources.
- No option accepts a secret.
- `has-breaking-change-marker` rejects an empty range or one that starts with `-`, and passes the range to Git after
  `--end-of-options`, so Git cannot read it as an option.
- Packing and consumption restore third-party packages from nuget.org. Generated NuGet configurations map
  `PandaBear.SharedKernel.*` only to the local feed, so internal packages never resolve from nuget.org.
- Candidate commands verify SHA-256 package hashes before consuming packages.

## Packaging and AOT

`PackAsTool=true`, `ToolCommandName=sharedkernel-version`, and `PackageReadmeFile=README.md` define the tool package.
The tool is analyzer-clean with `IsAotCompatible=true` and ships as a framework-dependent tool, not a RID-specific
native binary. Repository check for a native Linux x64 binary:

```text
dotnet publish tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj -c Release -r linux-x64 -p:PublishAot=true -p:IsRoslynComponent=true -o artifacts/sharedkernel-version-aot-check
```

`IsRoslynComponent=true` skips the repository analyzer project references so the NativeAOT publish property does not
apply to analyzer projects.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning.Tool`. C# namespace: `PandaBear.Versioning.Tool`. Assembly/project identity: `SharedKernel.Versioning.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
