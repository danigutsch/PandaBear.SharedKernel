# SharedKernel.Tooling

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Small, host-agnostic helpers that the SharedKernel command-line tools share. The package has no dependencies beyond
the .NET runtime, and is trimming- and NativeAOT-compatible.

## Scope

| Type | Use |
| --- | --- |
| `ExecutableResolver` | Resolves a command name only in the absolute directories of `PATH`, never in the application directory, the working directory, or a relative `PATH` entry, so a checkout cannot supply the executable. |
| `RepositoryPath` | Validates repository-relative paths from configuration files (`/` separators, no root, no empty, `.`, or `..` segments) and resolves them under a root without following symbolic links. |
| `ControlCharacters` | Escapes control, format, and separator characters and lone surrogates before text from files, processes, or the network reaches a terminal or CI log. |
| `CandidateManifest` | Finds, reads, verifies, and atomically writes the `candidate.json` that records a package candidate's revision, version, and SHA-256 package hashes. |

Command parsing, process runners, and GitHub clients stay in each tool or its engine library. Application policy
and data stay in the consuming repository's configuration files.

## Usage

```csharp
using PandaBear.Tooling;

var git = ExecutableResolver.ResolveCommand("git");
var script = RepositoryPath.Resolve(root, "tests/load/smoke.js");
Console.Error.WriteLine(ControlCharacters.Escape(untrustedLine));
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Tooling`. C# namespace: `PandaBear.Tooling`. Assembly/project identity: `SharedKernel.Tooling`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
