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

## Restore or install

Install from a local package during development:

```text
dotnet pack tools/SharedKernel.Testing.CodeFixRunner/SharedKernel.Testing.CodeFixRunner.csproj -o artifacts/local-tool-feed
dotnet tool install PandaBear.SharedKernel.Testing.CodeFixRunner --tool-path ./.tools --version <validated-candidate-version> --add-source artifacts/local-tool-feed
./.tools/sharedkernel-codefix --help
```

When installing from this repository, follow the local-feed source-mapping notes in
[release policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/release-policy.md).

Install as a global tool only from a trusted package source:

```text
dotnet tool install --global PandaBear.SharedKernel.Testing.CodeFixRunner --version <validated-candidate-version> --add-source <trusted-package-source>
sharedkernel-codefix --help
```

`.NET` tools run in full trust. Restore or install only from trusted manifests and package sources.

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
  fix and errors go to standard error.

## Exit codes

- `0`: the run finished, including when diagnostics without an available fix remain.
- `1`: invalid arguments, an unexpected error while loading or fixing the target, or cancellation.
- `2`: the project or solution did not load. No fix was applied.
- `130` (SIGINT) or `143` (SIGTERM): the signal arrived during start-up, or the run did not stop within two
  seconds of it.

## Packaging notes

- `PackAsTool=true` packages the console application as a .NET tool.
- `ToolCommandName=sharedkernel-codefix` controls the command used after install.
- `PackageReadmeFile=README.md` includes this usage guide in the NuGet package.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.CodeFixRunner`. C# namespace: `PandaBear.Testing.CodeFixRunner`. Assembly/project identity: `SharedKernel.Testing.CodeFixRunner`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
