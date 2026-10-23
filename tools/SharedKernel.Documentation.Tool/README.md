# SharedKernel.Documentation.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

.NET tool that refreshes generated Markdown blocks, detects stale blocks, and checks documented facts
against source files.

## Usage

Run from the repository root: the configuration path, and every path inside the configuration, resolve
against the current directory.

```text
sharedkernel-docs generate --config <path>
sharedkernel-docs verify --config <path>
sharedkernel-docs check --config <path>
```

| Command | Behavior |
| --- | --- |
| `generate` | Writes the generated Markdown blocks declared by a generator configuration and lists the changed files. |
| `verify` | Writes nothing. Lists stale files on standard error when any block differs from its generated content. |
| `check` | Writes nothing. Validates marked documentation facts against the sources a conformance configuration names. |

`--config` is required. Add `--help` to any command to describe its options.

## Limits and safety

- Every block declares one target Markdown file relative to the configured `docsPath`.
- Missing, duplicate, reversed, split, or misplaced generated markers fail before any file is written.
- Rooted targets, symbolic-link or junction paths, and generated marker text are rejected.
- `generate` replaces each changed file atomically.

The tool is a thin command line over `PandaBear.SharedKernel.Documentation`. Repository-specific system
maps, labels, source paths, and conformance checks belong in configuration.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Documentation was refreshed or is current, or every conformance check passed. |
| 1 | `verify` found stale documentation, generation or a conformance check failed, or the command line is invalid. |

Results go to standard output. Failures, stale-file lists, and command-line errors go to standard error.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Documentation.Tool`. C# namespace: `PandaBear.Documentation.Tool`. Assembly/project identity: `SharedKernel.Documentation.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
