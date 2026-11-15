# Tool engines and thin wrappers

Date: 2026-11-15. Status: accepted.

Most command-line tool logic lives inside `tools/`. It cannot be reused as a library, is tested only through the
command line, and shares helpers by linking four source files from `tools/Shared` into seven projects. Only the
Documentation, Packages, and Versioning tools already keep their logic in a `src` library. Issue
[#383](https://github.com/danigutsch/PandaBear.SharedKernel/issues/383) proposes generic engines in `src` and thin
tools, written as .NET file-based (single-file) apps.

## Decision

- **Engines live in `src/SharedKernel/SharedKernel.<Name>`.** An engine holds parsing, planning, validation, process
  and forge clients, and rendering. It is host-agnostic, trimming- and NativeAOT-compatible, documented like every
  other package (README, PublicAPI files, XML documentation), and takes the environment, process runner, and clock
  as injected delegates or interfaces. It exposes the smallest public API the wrapper needs; types that only its
  tests need stay internal, with `InternalsVisibleTo` for the test project.
- **A tool is a thin wrapper.** `tools/SharedKernel.<Name>.Tool` holds only the System.CommandLine tree, the wiring of
  real dependencies, output, and exit codes, and references its engine with a `ProjectReference`. Application policy
  and data still come from the consuming repository's configuration files and arguments.
- **Wrappers stay conventional `.csproj` projects, not file-based apps,** for the reasons under
  [Packaging evidence](#packaging-evidence).
- **Shared helpers are a library.** `ExecutableResolver`, `RepositoryPath`, `ControlCharacters`, and
  `CandidateManifest` move from `tools/Shared` to `src/SharedKernel/SharedKernel.Tooling` (package
  `PandaBear.SharedKernel.Tooling`, namespace `PandaBear.Tooling`) as a small public API. They are internal to no
  single tool, seven tool assemblies use them, and a public type is the way a packed library shares code with them;
  `InternalsVisibleTo` across separately versioned packages would be a hidden contract. `ControlCharacters.Escape` and
  `CandidateManifest.Write` now throw `ArgumentNullException` for a null argument where they used to throw
  `NullReferenceException`; nothing else changes.
- **`SourceRunnableToolConventionTests` and its rule are removed.** The rule kept `SharedKernel.CI.Tool` and
  `SharedKernel.RepoConfig.Tool` free of project references so that a sparse checkout of `tools/<tool>` and
  `tools/Shared` could build them. These tools now reference their engines normally. What stays a requirement is that
  a consumer can still build them from source before a candidate exists: the sparse checkout lists the tool directory
  and every `src/SharedKernel/<library>` it references. Cone mode also checks out the files directly inside each
  parent directory, so the root and `src` `Directory.Build.props` and `Directory.Build.targets` come along; the
  repository's own analyzer projects are referenced only when they exist, so the checkout does not need them.
- **Tools move one at a time,** each behind its existing command-line tests with no behavior change.
  `SharedKernel.PlaywrightMcp.Tool` is the pilot: its engine is `SharedKernel.PlaywrightMcp`, and the tool keeps
  `Program.cs` and the command tree. Sandbox, RepoConfig, CI, and VaultManager follow.
- **Engines are lock-exempt unless they add a package version.** A new engine with no package dependency of its own
  adds no `Id@Version` pair, so it is listed in `lockExemptProjects` and sets
  `RestorePackagesWithLockFile=false`, as [Lock files](../package-development.md#lock-files) requires. The tools and
  test projects keep their lock files, which now record the engine as a project dependency.

## Packaging evidence

The tools ship as `PandaBear.SharedKernel.*.Tool` packages: candidates pack them, record their SHA-256 hashes, and
install each with `--help` checks in `consume-candidate`. Every project restores in locked mode, and tool tests
reference the tool assembly to parse and invoke its real command tree in-process. Checked on 2026-11-15 against the
SDK in `global.json` (10.0.403) and the current documentation:

| Requirement | File-based app | Evidence |
| --- | --- | --- |
| Pack as a .NET tool | Yes. `PackAsTool` is on by default; `dotnet pack app.cs` produced a package with `tools/net10.0/any/DotnetToolSettings.xml`. | [File-based apps: Package as tool](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps#package-as-tool) |
| Locked restore with a lock file | Yes. With `#:property RestorePackagesWithLockFile=true`, `dotnet restore app.cs` wrote `packages.lock.json` beside the file, and `--locked-mode` failed with NU1004 after a version change. The lock file name is fixed per directory, so two apps cannot share one. | [dotnet restore: FILE argument](https://learn.microsoft.com/dotnet/core/tools/dotnet-restore#arguments) |
| NativeAOT publish | Yes. `PublishAot` is on by default; `dotnet publish app.cs` produced a native executable. | [File-based apps: Native AOT publishing](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps#native-aot-publishing) |
| Engine reference | Yes, with `#:project ../path/Engine.csproj`. | [File-based apps: `#:project`](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps#project) |
| Member of `SharedKernel.slnx` | No. `dotnet sln add app.cs` fails with MSB4025 ("The project file could not be loaded. Data at the root level is invalid."). The solution build, `dotnet format`, the governed-literal check, and CI test selection all work from the solution. | [dotnet sln add](https://learn.microsoft.com/dotnet/core/tools/dotnet-sln#add) takes project files only |
| Referenced by a test project | No. `<ProjectReference Include="../app.cs" />` fails with the same MSB4025 error, so the command-line contract tests could not call the tool's command factory in-process. | [File-based apps: Convert to project](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps#convert-to-project) is the documented path when a file-based app outgrows one file |
| Placement | File-based apps should not sit inside a project's directory cone, and inherit every `Directory.Build.props` above them. | [File-based apps: Folder layout recommendations](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps#folder-layout-recommendations) |

File-based apps meet the per-package requirements, but not the repository's: they cannot be solution members or
project references, so the solution build, formatting, test selection, and the in-process command-line tests would each
need a separate path for every tool. A thin `.csproj` costs one small XML file per tool and keeps every existing check.
The wrapper's code is the same either way, so a tool can become a file-based app later without moving logic, if a
future SDK lets a solution or a test project include one.

## Consequences

- A tool package carries its engine and `SharedKernel.Tooling` assemblies, as it already carried System.CommandLine.
  The engine is also its own preview package.
- Workflows and consumers that build `sharedkernel-ci` or `sharedkernel-repo` from a sparse checkout list
  `src/SharedKernel/SharedKernel.Tooling` instead of `tools/Shared`, and add each engine those tools come to reference.
- Engine tests can call the engine directly. The pilot keeps its tests in `SharedKernel.PlaywrightMcp.Tool.Tests`, as
  `SharedKernel.Versioning.Tests` covers both the Versioning library and tool.
