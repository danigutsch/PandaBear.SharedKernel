# SharedKernel.Versioning.Tool

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
sharedkernel-version pack-sharedkernel [--version <semver>] [--output-root <path>]
sharedkernel-version prepare-release --version <semver> --package-dir <path> < changes.txt
```

## Restore or install

Use this repository's local tool manifest when the tool is added there:

```text
dotnet tool restore
dotnet tool run sharedkernel-version -- --help
```

Install from a local package during development:

```text
dotnet pack tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj -o /tmp/opencode/sharedkernel-version-pack
dotnet tool install SharedKernel.Versioning.Tool --tool-path ./.tools --add-source /tmp/opencode/sharedkernel-version-pack
./.tools/sharedkernel-version --help
```

When installing from this repository, follow the local-feed source-mapping notes in
`docs/SHAREDKERNEL_PACKAGING.md`.

Install as a global tool only from a trusted package source:

```text
dotnet tool install --global SharedKernel.Versioning.Tool --add-source <trusted-package-source>
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

The `compute` command reads commit messages from standard input. Non-Conventional merge commits are
ignored; valid Conventional Commit messages drive the release impact. Use null-separated input when
feeding raw `git log` messages so multiline commit bodies stay grouped.

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
dotnet publish tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj -c Release -r linux-x64 -p:PublishAot=true -p:IsRoslynComponent=true -o /tmp/opencode/sharedkernel-version-aot-check
```

`IsRoslynComponent=true` skips repository analyzer project references during the publish check so the
Native AOT publish property is not applied to analyzer projects.
