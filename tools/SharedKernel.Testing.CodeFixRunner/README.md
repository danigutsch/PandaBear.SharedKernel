# SharedKernel.Testing.CodeFixRunner

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Local code-fix runner for SharedKernel testing analyzer migrations.

This package is a .NET tool. The command name is `sharedkernel-codefix`.

## Synopsis

```text
sharedkernel-codefix <project-or-solution> [--diagnostic <id>]
sharedkernel-codefix --help
sharedkernel-codefix --version
```

## Prerequisites

The tool runs on .NET 10 and loads the target through MSBuildLocator and MSBuildWorkspace. Each starts from
the .NET SDK that `dotnet` selects for a directory, honoring `global.json`:

- A .NET 10 SDK. MSBuildLocator loads MSBuild into the tool from the SDK selected for the current
  directory, or from the newest installed .NET 10 SDK when the selected SDK is newer than .NET 10.
- An SDK that can build the target. MSBuildWorkspace loads the target in a separate build host with the
  SDK selected for the target's directory.
- A restored target. Without restored packages, symbols such as xUnit's `[Fact]` do not resolve, and the
  run finds fewer diagnostics without reporting an error.

A `global.json` that applies to either directory must resolve to an installed SDK:

- For the current directory, commands that start the tool through the .NET SDK (`dotnet tool run`,
  `dotnet sharedkernel-codefix`, `dotnet run`) fail before the tool starts. The host reports that no compatible
  .NET SDK was found, or prints the `errorMessage` set in `global.json`, and exits 155 on Linux and macOS.
  Started directly, for example from a `--tool-path` install, the tool exits 2.
- For the target's directory, the run fails with exit code 1.

## Installation

Install a validated candidate as a local tool from the feed that holds it, such as the folder that
`sharedkernel-version stage-candidate` copies a candidate to. Add the feed to `NuGet.Config` and map
`PandaBear.SharedKernel.*` to it. Map every other package, for example `*`, to the existing source, because
a package without a mapping does not restore once source mapping is enabled. With the SDK pinned in
`global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.Testing.CodeFixRunner --version <validated-candidate-version>
dotnet tool run sharedkernel-codefix -- <project-or-solution>
```

In this repository, run the project without installing it:

```text
dotnet run --project tools/SharedKernel.Testing.CodeFixRunner -c Release -- <project-or-solution>
```

.NET tools run in full trust. Restore or install only from trusted manifests and package sources.

## Usage

Fix the default diagnostic, `SKTEST004`, in a project or solution:

```text
sharedkernel-codefix tests/SharedKernel.Versioning.Tests/SharedKernel.Versioning.Tests.csproj
```

The `SKTEST004` fix moves static helper methods without test-instance state to a dedicated helper
file. It skips local functions, nested helper types, overloaded helpers, and instance-dependent
helpers for manual review.

Fix another diagnostic:

```text
sharedkernel-codefix SharedKernel.slnx --diagnostic SKTEST006
```

- `<project-or-solution>` is an existing `.csproj`, `.sln`, or `.slnx` file.
- `--diagnostic` accepts the IDs the testing code-fix provider can fix; `--help` lists them.
- Fixes are written to source files in place. Run the tool on a clean working tree so the changes
  can be reviewed and reverted.
- Every project must load before any fix is applied. MSBuild errors and warnings while loading,
  missing projects, and project types Roslyn cannot open, such as `.esproj`, stop the run. Pass a
  `.csproj` to fix one project of such a solution.
- Ctrl+C or SIGTERM stops the run. Fixes already written stay on disk and may be unformatted.
- Standard output receives `Fixed <count> <id> diagnostic(s).`. Diagnostics without an available
  fix and errors go to standard error. When MSBuild cannot be loaded, the .NET host also writes its
  installed-SDK list to standard output.

## Exit codes

- `0`: the run finished, including when diagnostics without an available fix remain.
- `1`: invalid arguments, an unexpected error while loading or fixing the target, or cancellation.
- `2`: the project or solution did not load, or MSBuild could not be loaded from a .NET SDK for the current
  directory. No fix was applied.
- `130` (SIGINT) or `143` (SIGTERM): the signal arrived during start-up, or the run did not stop within two
  seconds of it.

## Packaging notes

- `PackAsTool=true` packages the console application as a .NET tool.
- `ToolCommandName=sharedkernel-codefix` controls the command used after install.
- `PackageReadmeFile=README.md` includes this usage guide in the NuGet package.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.CodeFixRunner`. C# namespace: `PandaBear.Testing.CodeFixRunner`. Assembly/project identity: `SharedKernel.Testing.CodeFixRunner`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
