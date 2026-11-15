# Tool engines and thin wrappers

Date: 2026-11-15. Status: accepted.

Most command-line tool logic lives inside `tools/`. It cannot be reused as a library, is tested only through the
command line, and shares helpers by linking four source files from `tools/Shared` into seven projects. Only the
Documentation, Packages, and Versioning tools already keep their logic in a `src` library. Issue
[#383](https://github.com/danigutsch/PandaBear.SharedKernel/issues/383) proposes generic engines in `src` and thin
tools, written as .NET file-based (single-file) apps.

## Decision

- **Engines live in `src/SharedKernel/SharedKernel.<Name>`, named by the capability or role they provide.** An engine
  holds parsing, planning, validation, process and forge clients, and rendering. It is host-agnostic, trimming- and
  NativeAOT-compatible, and documented like every other package (README, PublicAPI files, XML documentation). It
  exposes a small, intention-revealing API: a type named for its role, such as `PlaywrightMcpServer`, whose operations
  take a settings object, and hides the mechanics (argument building, engine selection) as internal types tested
  through `InternalsVisibleTo` for the library's own test project, `tests/SharedKernel.<Name>.Tests`.
- **An engine is a generic core library plus provider-specific libraries.** The core,
  `SharedKernel.<Capability>`, holds the logic that does not depend on a forge, vendor, or external program, and
  defines the seams (interfaces and settings) for the parts that do. Each provider implementation is a separate
  library, `SharedKernel.<Capability>.<Provider>`, such as `.GitHub`, `.Forgejo`, `.K6`, or `.Bitwarden`, so a consumer
  references only the providers it uses and a new provider adds a library instead of changing the core. The capability
  names the engine, not the tool: `sharedkernel-k6` can be a wrapper over a load-testing core and its k6 provider. A
  provider library exists only for an implementation verified against the provider's current documentation and
  covered by tests; an empty or speculative provider library is not created.
- **Platform base libraries hold the low-level access that several providers share.** `SharedKernel.GitHub` holds the
  GitHub token rules and the REST client headers, and every `.GitHub` provider builds on it; a Forgejo base,
  `SharedKernel.Forgejo`, would mirror it and is created by the first provider that needs it. A base library holds
  only client, authentication, and token concerns. A provider library depends on its feature core and its platform
  base; a platform base never depends on a feature library.
- **Names are feature first: `SharedKernel.<Feature>.<Provider>`,** such as `CI.GitHub`, `Repositories.GitHub`, or
  `LoadTesting.K6`. This mirrors the existing `SharedKernel.Npgsql` base with `EntityFrameworkCore.Npgsql`,
  `EventSourcing.Npgsql`, and `Observability.Npgsql`, and `ObjectStorage.S3` and `MalwareScanning.ClamAv`.
  Platform-first names (`GitHub.CI`) would invert that convention, and a platform base that depended on feature
  libraries (`GitHub` on `CI`) would drag every feature into any consumer of the client.
- **A tool is a thin wrapper.** `tools/SharedKernel.<Name>.Tool` holds only the System.CommandLine tree, the wiring of
  real dependencies, reading the settings, output, and exit codes, and references its engine with a
  `ProjectReference`. Application policy and data still come from the consuming repository's configuration files and
  arguments.
- **Wrappers stay conventional `.csproj` projects, not file-based apps,** for the reasons under
  [Packaging evidence](#packaging-evidence).
- **Shared helpers go to the library that owns the concept,** not to a grab-bag. The four `tools/Shared` files become
  public API, because seven tool assemblies use them and `InternalsVisibleTo` across separately versioned packages
  would be a hidden contract:
  - `ExecutableResolver` becomes `IExecutableResolver` and `PathExecutableResolver` in a new
    `SharedKernel.Processes` library, with process execution (below).
  - `RepositoryPath` becomes a value type in `SharedKernel.IO`, which owns file and stream concepts: `IsValid`,
    `TryParse`, `Parse`, and `ResolveUnder(root)`.
  - `ControlCharacters` joins `SharedKernel.InputNormalization`, which already sanitizes untrusted text; `Escape` is a
    pure function and stays static.
  - `CandidateManifest` joins `SharedKernel.Versioning`, which owns candidates and release artifacts, as a class with
    `Find`, `Load` (read and verify the package hashes), `Evidence`, and `Save`.
  `ControlCharacters.Escape` and saving a manifest throw `ArgumentNullException` for a null argument where they used to
  throw `NullReferenceException`; the tools' observable behavior is otherwise unchanged.
- **`SourceRunnableToolConventionTests` and its rule are removed.** The rule kept `SharedKernel.CI.Tool` and
  `SharedKernel.RepoConfig.Tool` free of project references so that a sparse checkout of `tools/<tool>` and
  `tools/Shared` could build them. These tools now reference their engines normally. What stays a requirement is that
  a consumer can still build them from source before a candidate exists. It checks out the fixed cone `src` and
  `tools` of the revision, so the checkout needs no list of the libraries a tool references (see [Shared capabilities
  and source builds](#shared-capabilities-and-source-builds)).
- **Tools move one at a time,** each behind its existing command-line tests with no behavior change.
  `SharedKernel.PlaywrightMcp.Tool` is the pilot: its engine is `SharedKernel.PlaywrightMcp`, and the tool keeps
  `Program.cs` and the command tree. Sandbox, RepoConfig, CI, and VaultManager follow.
- **Side effects sit behind small abstractions in `SharedKernel.Processes`.** `IProcessRunner` runs a
  `ProcessStartInfo` and returns a `ProcessOutput`, optionally forwarding each output line to a writer.
  `SystemProcessRunner` runs it as described and kills the process tree on cancellation; `BoundedProcessRunner` adds a
  two-minute limit and output limits for quick queries. `IExecutableResolver.Find` returns an executable from the
  absolute `PATH` directories. `SharedKernel.Packages` had its own `IProcessRunner`, `ProcessOutput`, and
  `SystemProcessRunner`; they are replaced by these, and Packages builds its start information (the `C` locale, an
  empty standard input) and passes it to the shared runner. Engines take the abstractions through their constructors;
  hosts pass the system implementations and tests pass fakes. Diagnostics go to a `TextWriter` that the host passes,
  because System.CommandLine already supplies one per invocation; environment variables are read by the tool into a
  settings object, not by the engine.
- **Logic that two or more tools need lives in a shared capability library,** not in either engine. See
  [Shared capabilities and source builds](#shared-capabilities-and-source-builds).
- **Engines are lock-exempt unless they add a package version.** A new engine with no package dependency of its own
  adds no `Id@Version` pair, so it is listed in `lockExemptProjects` and sets
  `RestorePackagesWithLockFile=false`, as [Lock files](../package-development.md#lock-files) requires. The tools and
  test projects keep their lock files, which now record the engine as a project dependency.

## Shared capabilities and source builds

A survey of every tool before the engine extractions found the same mechanics copied between them. They moved to
capability libraries, each with its own tests, so the engines start from one implementation:

- `SharedKernel.ConfigurationFiles` (new): `JsonConfigurationFile` reads a strict JSON consumer configuration file
  under a root, as five tools did with identical messages. The library and namespace say "configuration files"
  because a `PandaBear.Configuration` namespace would shadow every `Configuration` identifier inside `PandaBear.*`
  code (CS0118).
- `SharedKernel.GitHub` (new): `GitHubToken` (find `GH_TOKEN` then `GITHUB_TOKEN`, refuse forwarding them, remove them
  from a child process) and `GitHubApiClient` (REST headers), which CI, K6, and RepoConfig each implemented.
- `SharedKernel.Processes`: `ProcessTree` (start, and end a process tree with a bounded wait) and `ProcessEnvironment`
  (remove `GIT_*` or named variables), copied in CI, K6, Versioning, and four RepoConfig classes. `ProcessEnvironment`
  also removes every forge token variable (`GH_TOKEN`, `GITHUB_TOKEN`, `FORGEJO_TOKEN`) from a child, so an engine that
  only keeps tokens away from its children does not reference a forge platform base for it.
- `SharedKernel.IO`: `BoundedReader` (whole file or stream up to a limit, `null` above it).
- `SharedKernel.InputNormalization`: `ControlCharacters.EscapeLines` for multi-line messages.

An engine change consumes these libraries and does not extend them; logic that a second tool comes to need moves to
the library that owns its concept in a change of its own.

`sharedkernel-ci` and `sharedkernel-repo` build from a checkout of the revision before a candidate exists. A sparse
cone listing `tools/<tool>` and each referenced `src/SharedKernel/<library>` had to grow with every engine, here and in
each consumer. The checkout is now the fixed cone `src` and `tools` at depth 1 (plus `.github` where a job uses the
repository's actions or action pin manifest), which holds every library and tool, whatever they reference. Measured
on 2026-11-15 at the revision that merged the pilot: the cone is 13 MB of files against 6 MB for the per-library cone
and 32 MB for the whole revision (a 4 MiB pack); each fetches and checks out in under half a second locally, and a
tool builds in 3 to 14 seconds in each, dominated by restore and compiler start. The cone also builds the
repository's style analyzers, as every other build here does. The whole revision is not an option on Windows:
generated files under `samples/` and `tests/` have paths of up to 206 characters, which with a runner workspace prefix
pass the 260-character limit, and `actions/checkout` fails with "Filename too long". The longest path under `src` is
145 characters and under `tools` 86; a convention test may cap them later if they grow.

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

- A tool package carries the assemblies of the libraries it references, as it already carried System.CommandLine.
  Each library is also its own preview package.
- Workflows and consumers that build `sharedkernel-ci` or `sharedkernel-repo` from source check out the fixed cone
  `src` and `tools` instead of a per-library cone, so a new engine reference changes no workflow or consumer.
- `PandaBear.SharedKernel.Packages` changes its public API: `IProcessRunner`, `ProcessOutput`, and `SystemProcessRunner`
  move to `PandaBear.Processes`, and the bounded runner is `BoundedProcessRunner`. Packages are preview, so this is
  allowed, and consumers update their namespace and runner type.
- Engine unit tests live in the library's own test project, such as `tests/SharedKernel.PlaywrightMcp.Tests`, and
  use fakes of the abstractions; the tool's test project keeps the command-line contract tests.
