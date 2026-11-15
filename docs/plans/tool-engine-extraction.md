# Tool engine extraction map

Date: 2026-11-15. Applies the [tool engines ADR](../adr/20261115-tool-engines-and-thin-wrappers.md) to every tool under
`tools/`. Phase 1 surveyed the tools and moved the logic that two or more tools duplicated into shared libraries.
Phase 2 moves each tool's remaining logic into its engine, one owner per tool, so that no two owners edit the same
library.

## Survey: what the tools duplicated

| Concern | Where it was duplicated | Phase 1 result |
| --- | --- | --- |
| Strict JSON consumer configuration: resolve `.config/<tool>.json` under the root without symbolic links, read at most 1 MiB, deserialize with a source-generated context, report `is not valid at <path> (line <n>)` and `must contain a JSON object` | CI, K6, Packages, Sandbox, RepoConfig (five copies, identical messages) | `JsonConfigurationFile` in the new `SharedKernel.ConfigurationFiles` |
| Whole-file and whole-stream reads up to a limit | CI `BoundedFile`, RepoConfig `RepositoryFile.Read` and `BoundedHttpContentReader`, CI release-list body, inline loops in K6, Packages, Sandbox | `BoundedReader` in `SharedKernel.IO` |
| GitHub token lookup (`GH_TOKEN`, then `GITHUB_TOKEN`), token names refused in configuration, token removal from child processes | CI (pruning, configuration, service host), RepoConfig (`GitHubHttpClient`, `ChildProcessEnvironment`), K6 (configuration, `ChildProcess`) | `GitHubToken` in the new `SharedKernel.GitHub` |
| GitHub REST client headers (user agent, `Accept`, `X-GitHub-Api-Version`, bearer token) | CI prerelease pruning, RepoConfig `GitHubHttpClient` | `GitHubApiClient` in `SharedKernel.GitHub` |
| Kill a process tree, wait a bounded time, ignore cleanup failures | CI `ProcessRunner`, K6 `ChildProcess`, Versioning `CommandRunner`, RepoConfig (`GitTrackedFiles`, `RoadmapGitRepository`, `TextEncodingVerifier`, `PodmanDnsPreflightCommand`), `SystemProcessRunner` | `ProcessTree.Terminate` in `SharedKernel.Processes` |
| Start a process and report a start failure as `InvalidOperationException` | CI, K6, Versioning | `ProcessTree.Start` |
| Remove inherited `GIT_*` variables before running Git | CI `ProcessRunner`, RepoConfig (`GitTrackedFiles`, `BaselineGit`, `TextEncodingVerifier`) | `ProcessEnvironment.RemoveGitVariables` |
| Write an exception message line by line, each line escaped | CI (command line and run evidence), K6, Packages | `ControlCharacters.EscapeLines` in `SharedKernel.InputNormalization` |

Looked at and left in place, because the copies differ in behavior that callers rely on, or only one tool has them:

- **System.CommandLine helpers.** `ParseWholeNumber` exists only in CI; RepoConfig's `--limit` parser and path parsers
  word their errors differently, and the README-documented messages are part of each command-line contract. Each
  tool's guarded action catches its own exception set, so the shared part is `EscapeLines`. No shared
  System.CommandLine library was created.
- **Run-and-capture runners.** CI `ProcessRunner` (Git timeout, timestamped echo) and Versioning `CommandRunner`
  (streaming with environment overrides) both format `Command failed (<code>): ...`, but their timeouts, echo, and
  streaming differ. Their engines (phase 2) move them onto `IProcessRunner` and keep the message.
- **Atomic file replacement.** Sandbox `SettingsFileStore` (compare-and-swap, keeps the file mode), VaultManager
  `SecureFile` (owner-only mode, secret cleanup), and RepoConfig `RollbackFileWriteBatch` (multi-file rollback) have
  different contracts.
- **Hashing, temporary directories, environment reads.** One-line BCL calls (`SHA256.HashData`,
  `Directory.CreateTempSubdirectory`); environment variables are read by the tool into settings, as the ADR requires.
- **GitHub Actions outputs.** CI, RepoConfig, and Versioning write different files in different formats; RepoConfig's
  `automation` steps read `GITHUB_OUTPUT` by design.
- **Repository root discovery.** Every tool takes `--root` (or `--repo-root`) and defaults to the working directory;
  nothing searches upward.

## Shared libraries (phase 1 owns them; phase 2 only consumes)

| Library | Public API | Used by |
| --- | --- | --- |
| `SharedKernel.ConfigurationFiles` (new) | `JsonConfigurationFile.Read<T>(root, relativePath, typeInfo, ct)`, `ReadOptional<T>`, `Parse<T>(content, description, typeInfo)`, `MaxBytes` | CI, K6, Packages, Sandbox, RepoConfig |
| `SharedKernel.GitHub` (new) | `GitHubToken.VariableNames`, `Find(getEnvironmentVariable)`, `IsTokenVariable(name)`, `RemoveFrom(startInfo)`; `GitHubApiClient.Create(handler, userAgent, token)`, `ApiVersion` | CI, K6, RepoConfig |
| `SharedKernel.Processes` (extended) | `ProcessTree.Start(startInfo)`, `ProcessTree.Terminate(process, exitTimeout)`, `ProcessEnvironment.Remove(startInfo, names)`, `ProcessEnvironment.RemoveGitVariables(startInfo)`, plus the existing runners and resolver | CI, K6, Versioning, RepoConfig, PlaywrightMcp, Packages |
| `SharedKernel.IO` (extended) | `BoundedReader.ReadFile(path, maxBytes, ct)`, `BoundedReader.ReadToEnd(stream, maxBytes, ct)`, plus `RepositoryPath` | all configuration readers, CI, RepoConfig |
| `SharedKernel.InputNormalization` (extended) | `ControlCharacters.EscapeLines(value)`, plus `Escape` | CI, K6, Packages |

A phase 2 owner that needs a new shared capability keeps it internal to its engine and reports it, instead of editing
one of these libraries; phase 1 (or a later shared pass) promotes it once a second tool needs it.

## Extraction map (phase 2)

Every engine follows the ADR: `src/SharedKernel/SharedKernel.<Name>`, namespace `PandaBear.<Name>`, the
`PlaywrightMcp`/`ObjectStorage` project layout, PublicAPI files, a README, `InternalsVisibleTo` only for
`tests/SharedKernel.<Name>.Tests`, lock-exempt unless it adds a package version no retained lock covers, a solution
entry, and an architecture diagram regeneration. The engine name is the tool name without `.Tool`, as for the existing
`Documentation`, `Packages`, `Versioning`, and `PlaywrightMcp` engines. An owner may split its engine into
`SharedKernel.<Name>.*` libraries; that prefix is its alone. Each owner edits only its tool project, its tool's test
project, its engine libraries and their test projects, and its own README and documentation sections.

| Owner | Tool (`tools/`) | Engine library | Depends on (consume only) | Planned public API, high level |
| --- | --- | --- | --- | --- |
| A | `SharedKernel.CI.Tool` | `SharedKernel.CI` (new) | ConfigurationFiles, GitHub, InputNormalization, IO, Processes, Versioning | `CiConfiguration` + reader; `TestSelector` (select plan from a base revision and the solution graph); `SliceRunner` (restore, build, services, test lanes, evidence); `CoverageGate`; `WarningReports`; `CandidateStager` and `PrereleasePruner` (over an injected `HttpMessageHandler` and `GitHubToken`); `CommitMessageVerifier`; `LocalCheck`; `OpenApiGenerator`; `MutationTestRunner`. Git and dotnet through `IProcessRunner`. |
| B | `SharedKernel.RepoConfig.Tool` | `SharedKernel.RepoConfig` (new; may split into `SharedKernel.RepoConfig.*`, such as `.Roadmap` and `.Devcontainers`) | ConfigurationFiles, GitHub, InputNormalization, IO, Processes | `ConsumerConfiguration` + reader; `BaselinePlanner`/`BaselineApplier`; verifiers (links, Gherkin, JSON, text encoding, line endings, action pins, Dependabot ignores, license exceptions); `DependencySnapshot`/`DependencySubmitter`; `SecurityScanner`; `LabelSynchronizer`; roadmap automation and GitHub project sync; devcontainer and Podman DNS checks; `RepositoryInitializer` (`new`). |
| C | `SharedKernel.VaultManager.Tool` | `SharedKernel.VaultManager` (new; may split into `SharedKernel.VaultManager.*`) | BuildingBlocks, InputNormalization, IO | Vault export readers and writer (Bitwarden, Keeper, CSV), `DuplicateMatcher`, `Merger` and decisions, `SecretScanner`/`SecretPlacer`, catalog and brief builders, password health. The Terminal.Gui review UI and password prompts stay in the tool; Argon2 (`Konscious.Security.Cryptography.Argon2`) moves with the crypto code. Must not use the name `SharedKernel.Secrets`, which the open secrets-engine work takes. |
| D | `SharedKernel.Sandbox.Tool` | `SharedKernel.Sandbox` (new) | ConfigurationFiles, InputNormalization, IO | `ISandboxEnvironment` + system implementation; `SandboxDoctor` (probes and report); `SettingsPlanner`/`SettingsMerger`/`SettingsFileStore` with `WritePathPolicy` and risk flags. |
| E | `SharedKernel.K6.Tool` | `SharedKernel.K6` (new) | ConfigurationFiles, GitHub, InputNormalization, IO, Processes | `K6Configuration` + reader; `K6Runner` (host process, container or local k6, summary) over `IProcessRunner`/`IExecutableResolver` and an injected `HttpMessageHandler` for health checks. Must not edit `SharedKernel.Aspire.Hosting.K6`. |
| F | `SharedKernel.Packages.Tool` | `SharedKernel.Packages` (existing) | ConfigurationFiles, InputNormalization, Processes | Move the configuration reader, `plan`, and `updates` (update sources, minimum release age) into the engine. |
| G | `SharedKernel.Versioning.Tool` | `SharedKernel.Versioning` (existing) | IO, Processes | Move the pack, candidate, consumer verification, tool package check, lock coverage, and API compatibility commands' logic into the engine; replace `CommandRunner` with `IProcessRunner`. Owner A consumes Versioning's public API and asks owner G for changes. |
| — | `SharedKernel.PlaywrightMcp.Tool` | `SharedKernel.PlaywrightMcp` (existing) | Processes | Done in the pilot. |
| — | `SharedKernel.Documentation.Tool` | `SharedKernel.Documentation` (existing) | — | Already thin (two files): no phase 2 work. |
| — | `SharedKernel.Testing.CodeFixRunner` | none | — | Not extracted. Its logic is already separated in `CodeFixRunEngine`, and an engine library would make `Microsoft.CodeAnalysis.Workspaces.MSBuild` and `Microsoft.Build.Locator`, which must register before MSBuild types load, a library dependency. The tool's name is also its package ID, so an engine could not take it. Revisit only with a separate decision. |

Phase 2 owners keep every command, option, exit code, output, and README command line unchanged, and the tool's
command-line tests pass with unchanged assertions; engine unit tests go to the engine's own test project. Owners A
and B need no workflow or consumer change for their new engine references, because source builds check out the fixed
`src` and `tools` cone.

## Building CI and RepoConfig from source

Workflows and consumers build `sharedkernel-ci` and `sharedkernel-repo` from a checkout of the revision before a
candidate exists. Each new engine reference used to add a `src/SharedKernel/<library>` line to every sparse checkout
list here and in consumers. The checkout is now the fixed cone `src` and `tools` (plus `.github` where needed), so the
lists need no maintenance. The whole revision does not check out on Windows because of long generated paths under
`samples/` and `tests/`. See [Running before a candidate is staged](../command-line-tools.md#running-before-a-candidate-is-staged).
