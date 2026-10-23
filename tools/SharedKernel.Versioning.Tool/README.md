# SharedKernel.Versioning.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain `<target>-alpha.<run>.<attempt>` previews; stable packages, tags, and releases require separate authorization. `calculate-release --version-kind prerelease` requires `--run-number`. Without a reachable `v*` tag the target is `0.1.0`. When the latest reachable `v*` tag is a prerelease, its version core is the fixed target and only the prerelease identifier advances; after a stable tag, the Conventional Commit impact selects the next target on each run until a prerelease tag fixes it. `--version-kind stable` releases the same target core. Commit impact is always reported. The generic `compute` command applies normal SemVer bumps.

Local and CI versioning automation for SharedKernel release flows.

This package is a .NET tool. The command name is `sharedkernel-version`.

The command-line shape follows common CLI documentation conventions:

- synopsis first
- commands and options next
- standard `--help` and `--version` options
- standard output for machine-readable command output
- standard error for diagnostics
- documented exit codes

## Synopsis

```text
sharedkernel-version --help
sharedkernel-version --version
sharedkernel-version commit-impact <message>
sharedkernel-version compute --base <version> [--prerelease <label>] [--sha <sha>] < commit-messages.txt
sharedkernel-version calculate-release [--repo-root <path>] [--version-kind <prerelease|stable>]
sharedkernel-version check-public-api-baselines [--repo-root <path>]
sharedkernel-version has-breaking-change-marker <git-range> [--repo-root <path>]
sharedkernel-version api-compatibility [--version <semver>] [--release-phase <alpha|beta|rc|stable>] [--baseline-version <semver>] [--output-root <path>] [--breaking-marker] [--repo-root <path>]
sharedkernel-version pack-sharedkernel [--version <semver>] [--output-root <path>]
sharedkernel-version prepare-release --version <semver> --package-dir <path> < changes.txt
```

## Restore or install

This repository builds the tool directly; no local tool manifest is required:

```text
dotnet run --project tools/SharedKernel.Versioning.Tool -c Release -- --help
```

Install from a local package during development:

```text
dotnet pack tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj -o artifacts/local-tool-feed
dotnet tool install PandaBear.SharedKernel.Versioning.Tool --tool-path ./.tools --version <validated-candidate-version> --add-source artifacts/local-tool-feed
./.tools/sharedkernel-version --help
```

When installing from this repository, follow the local-feed source-mapping notes in
[release policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/release-policy.md).

Install as a global tool only from a trusted package source:

```text
dotnet tool install --global PandaBear.SharedKernel.Versioning.Tool --version <validated-candidate-version> --add-source <trusted-package-source>
sharedkernel-version --help
```

`.NET` tools run in full trust. Restore or install only from trusted manifests and package sources.

## Commands

Print help:

```text
sharedkernel-version --help
```

Print version:

```text
sharedkernel-version --version
```

Calculate release impact for one Conventional Commit message:

```text
sharedkernel-version commit-impact "feat(versioning): emit JSON output"
```

Calculate version output from commit history:

```text
git log --format=%B%x00 v0.1.0..HEAD | sharedkernel-version compute --base 0.1.0 --prerelease alpha.1 --sha abc123
```

Output fields:

- `semVer`
- `releaseImpact`
- `packageVersion`
- `assemblyVersion`
- `fileVersion`
- `informationalVersion`

GitHub Actions output names use snake case: `sem_ver`, `release_impact`, `package_version`,
`assembly_version`, `file_version`, `informational_version`, and `source_tag`. Library release workflows use
`package_version` for immutable package identity. Application container tagging and deployment remain
consumer-owned.

The `compute` command reads commit messages from standard input. Non-Conventional merge commits are
ignored; valid Conventional Commit messages drive the release impact. Use null-separated input when
feeding raw `git log` messages so multiline commit bodies stay grouped.

Check that every `src/SharedKernel/SharedKernel.*` package project has public API baseline files:

```text
sharedkernel-version check-public-api-baselines
```

Detect Conventional Commit breaking-change markers in commit history:

```text
sharedkernel-version has-breaking-change-marker origin/main..HEAD
```

Generate the API compatibility report and, when a baseline version is supplied, pass the official .NET
package validation properties to `dotnet pack`:

```text
sharedkernel-version api-compatibility --version 0.1.0-alpha.1 --release-phase alpha --output-root artifacts/api-compat
sharedkernel-version api-compatibility --version 0.2.0-beta.1 --release-phase beta --breaking-marker --baseline-version 0.1.0 --repo-root .
sharedkernel-version api-compatibility --version 1.0.0 --release-phase stable --baseline-version 0.9.0
```

The command uses these internal environment variables while invoking `pack-sharedkernel`:

- `API_COMPAT_ENABLE_PACKAGE_VALIDATION` -> `EnablePackageValidation`
- `API_COMPAT_BASELINE_VERSION` -> `PackageValidationBaselineVersion`

The variables are restored after the command completes or fails. They are an implementation detail for
sharing options between commands, not a supported contributor interface.

## Exit codes

- `0`: command succeeded.
- `2`: invalid command, invalid arguments, or invalid version/commit input.

Errors are written to standard error. Command output is written to standard output so CI scripts can
pipe or parse it safely.

## Packaging notes

- `PackAsTool=true` packages the console application as a .NET tool.
- `ToolCommandName=sharedkernel-version` controls the command used after install.
- `PackageReadmeFile=README.md` includes this usage guide in the NuGet package.

## AOT compatibility

The tool is analyzer-clean with `IsAotCompatible=true` and avoids runtime code generation and
dynamic loading. This NuGet package is distributed as a standard .NET tool, not as a RID-specific
native binary. Producing native binaries requires a separate `dotnet publish -r <RID>` distribution
flow.

Repository check for a native Linux x64 binary:

```text
dotnet publish tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj -c Release -r linux-x64 -p:PublishAot=true -p:IsRoslynComponent=true -o artifacts/sharedkernel-version-aot-check
```

`IsRoslynComponent=true` skips repository analyzer project references during the publish check so the
Native AOT publish property is not applied to analyzer projects.

## Private candidate workflow

`validate-candidate [--version <unique-prerelease>] [--aot]` packs and validates a clean committed repository, recording revisions and package hashes.
`consume-candidate --candidate <candidate.json> [--output <evidence.json>] [--aot]` verifies those hashes and consumes the same packages from their
current location with isolated caches. `smoke-packages --feed <directory> --version <version> [--aot]` performs package checks directly. Application
compatibility evidence is recorded separately from library validation.

## Stage a pinned local candidate

`stage-candidate --candidate <manifest> --feed <source-directory> --output <local-feed> --repo-root <library-checkout>`
verifies the pinned library revision and package hashes before copying packages. Existing different
artifacts are never overwritten. Repeating staging with identical artifacts is safe. Cancellation removes
incomplete temporary copies; previously verified packages remain reusable. No package registry is contacted
by staging. Assemblies remain `SharedKernel.*`; C# namespaces use `PandaBear.*`; package IDs use `PandaBear.SharedKernel.*`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning.Tool`. C# namespace: `PandaBear.Versioning.Tool`. Assembly/project identity: `SharedKernel.Versioning.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.


`create-candidate [--version <unique-prerelease>]` performs API/package checks and writes the immutable
candidate manifest without platform consumption. CI calls it once and sends those same hashed
packages to Linux and Windows `consume-candidate --candidate-directory <directory> --aot` jobs.
`validate-candidate` retains its combined create-and-consume behavior for local callers. Candidate
creation still requires a clean committed checkout; platform evidence remains pending until consumed.
