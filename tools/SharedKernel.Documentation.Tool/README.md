# SharedKernel.Documentation.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Local .NET tool for generated documentation checks and refreshes.

## Usage

```text
dotnet run --project tools/SharedKernel.Documentation.Tool -- generate --config docs/architecture/generated-diagrams.json
dotnet run --project tools/SharedKernel.Documentation.Tool -- generate --config docs/architecture/generated-diagrams.json --check
```

The standalone repository invokes the .NET command directly; generated configuration belongs to this repository. Application-only conformance rules remain consumer-owned.

## Command behavior

- `generate` refreshes all configured generated Markdown blocks.
- `--check` detects drift and exits non-zero without writing files.
- `--config` points to the repository-owned generator config file.
- Every block declares one target Markdown file relative to the configured `docsPath`.
- Missing, duplicate, reversed, split, or misplaced generated markers fail before any file is written.
- Rooted targets, symbolic-link or junction paths, and generated marker text are rejected.
- `check` validates machine-readable documentation facts against the configured source files without
  writing documents.

The tool is a thin CLI over `src/SharedKernel/SharedKernel.Documentation`; it contains no parsing or
ViajantesTurismo-specific rules. Repository-specific system maps, labels, source paths, and
conformance checks belong in config.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Documentation.Tool`. C# namespace: `PandaBear.Documentation.Tool`. Assembly/project identity: `SharedKernel.Documentation.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
