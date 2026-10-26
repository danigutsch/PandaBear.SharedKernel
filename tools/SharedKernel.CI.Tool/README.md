# SharedKernel CI tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-ci` selects the test projects affected by a change, as the repository's `.config/sharedkernel-ci.json`
configures, writes a solution filter for each CI test slice, runs the selected test projects of one or more slices
through one solution filter with the slices' services running around the tests, generates OpenAPI documents, installs
Playwright browsers, verifies the coverage of changed code, stages pinned package candidates in a local feed, verifies
Conventional Commit messages in pull request events and git commit message files, and runs Stryker.NET mutation tests.

## Prerequisites

Git and the .NET SDK pinned in `global.json`. `select` reads `.config/sharedkernel-ci.json` and the solution it names in a
Git checkout, and `run` reads the configuration and the plan that `select` wrote; `openapi` reads the configuration;
`coverage` reads the configuration in a Git checkout with the history of `--base`;
`install-playwright`, and `run --install-playwright`, need PowerShell (`pwsh`);
`run` for a slice with a `service` starts the service's project with `dotnet run`, so it needs whatever that project
needs, such as a container runtime;
`stage-candidate` reads a clean SharedKernel checkout and, with `--repository`, needs HTTPS access to `github.com` and
`release-assets.githubusercontent.com`; `verify-commit-message` reads only its event or message file;
`run-mutation-tests` reads the configuration and, until the configured `dotnet-stryker` is installed, needs a NuGet
source, as the root's `NuGet.Config` names it, that serves the `dotnet-stryker` package.

## Installation

Install a validated candidate as a local tool from the feed that holds it, such as the folder that
`sharedkernel-ci stage-candidate` stages a candidate in. Add the feed to `NuGet.Config` and map
`PandaBear.SharedKernel.*` to it. Map every other package, for example `*`, to the existing source, because
a package without a mapping does not restore once source mapping is enabled. With the SDK pinned in
`global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.CI.Tool --version <validated-candidate-version>
dotnet tool run sharedkernel-ci -- verify-commit-message --file <commit-message-file>
```

.NET tools run in full trust. Install only from trusted manifests and package sources.

The tool also builds from a cone sparse checkout of `tools/SharedKernel.CI.Tool` and `tools/Shared`, for jobs that
run before a candidate exists. Its only package dependency is System.CommandLine. In this repository, run the project
without installing it:

```sh
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- select --base <object-id>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- run --slice <name>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- openapi refresh --target <name>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- install-playwright --project <test-project> --browser chromium --with-dependencies
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- stage-candidate --pin <candidate.json> --library-root . --output <feed> --repository danigutsch/PandaBear.SharedKernel
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- verify-commit-message --event <event.json>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- run-mutation-tests
```

Package ID: `PandaBear.SharedKernel.CI.Tool`. C# namespace: `PandaBear.CI.Tool`. Assembly/project identity:
`SharedKernel.CI.Tool`. Source is public; registry publication remains deferred, and nothing here implies
availability on nuget.org.

## Usage

`--help` lists the options of each command. `--root` sets the repository root (default: the current directory);
the other relative paths of `select`, `run`, `install-playwright`, `coverage`, and `stage-candidate` resolve from it, and relative `--event` and
`--file` paths resolve from the current directory. A command line that starts with `[diagram]` prints how it parses
instead of running; an invalid command line exits 1.

### Configuration

`select`, `run`, `openapi`, `coverage`, and `run-mutation-tests` read `<root>/.config/sharedkernel-ci.json`: strict JSON of at most 1 MiB with camelCase property names that
match exactly. Comments, trailing commas, and unknown, repeated, or missing properties are errors, and every error names
the file and the JSON path of the problem.

```json
{
  "version": 1,
  "solution": "App.slnx",
  "selection": {
    "fullValidation": [".github/*", "*.props", "*.targets", "src/Shared/*"],
    "documentation": ["docs/*", "README.md"],
    "alwaysSelected": ["tests/Architecture.Tests/Architecture.Tests.csproj"],
    "requireCoveringTests": true,
    "testPaths": [
      { "paths": ["config/*"], "tests": ["tests/Config.Tests/Config.Tests.csproj"] }
    ]
  },
  "slices": [
    { "name": "fast", "projects": [] },
    {
      "name": "system",
      "projects": ["tests/System.Tests/System.Tests.csproj"],
      "openApi": ["admin"],
      "playwright": { "project": "tests/System.Tests/System.Tests.csproj", "browsers": ["chromium"] }
    },
    {
      "name": "database",
      "projects": ["tests/Database.Tests/Database.Tests.csproj", "tests/Outbox.Tests/Outbox.Tests.csproj"],
      "service": {
        "project": "tests/Database.Host/Database.Host.csproj",
        "readyFileVariable": "APP_DATABASE_READY_FILE",
        "valueVariable": "APP_DATABASE_CONNECTION_STRING",
        "readyTimeoutSeconds": 300,
        "stopTimeoutSeconds": 60
      }
    }
  ],
  "defaultSlice": "fast",
  "openApi": [
    {
      "name": "admin",
      "project": "src/Admin.Api/Admin.Api.csproj",
      "generateProperty": "GenerateAdminOpenApiArtifacts",
      "refreshProperty": "RefreshAdminOpenApiArtifacts",
      "preserveEnvironment": ["REMOTE_CONTAINERS"]
    }
  ],
  "coverage": {
    "thresholdPercent": 80,
    "include": ["src/*.cs", "src/*.razor"],
    "exclude": ["*/Migrations/*", "*/obj/*", "*.g.cs", "*.generated.cs"],
    "symbolDirectories": ["src"]
  },
  "mutation": {
    "strykerVersion": "4.15.0",
    "configFiles": ["tests/Domain.Tests/stryker-config.json", "tests/Application.Tests/stryker-config.json"]
  }
}
```

| Property | Meaning |
| --- | --- |
| `version` | `1`. |
| `solution` | The `.slnx` whose projects are selected, relative to the root. |
| `selection.fullValidation` | Path patterns whose change selects every test project. |
| `selection.documentation` | Path patterns whose change selects nothing while every tracked build file is inert. |
| `selection.alwaysSelected` | Test projects that run whenever a change selects any project. |
| `selection.testPaths[].paths` | Optional section. Path patterns, at least one, of files that no solution project owns and that only some test projects read. |
| `selection.testPaths[].tests` | The solution test projects, at least one, that a change of a matching path selects, plus `alwaysSelected`. |
| `selection.requireCoveringTests` | Whether a changed project that no selected test project depends on selects every test project. |
| `slices[].name` | Lowercase letters and digits in words separated by `-`, other than `build`. It names the matrix entry, the filter, and the `<name>_required` output. |
| `slices[].projects` | The slice's test projects. Each is a test project of the solution, listed in one slice. Only the default slice may list none. |
| `slices[].openApi` | Optional names of `openApi` targets that the slice generates after its build and before its tests run. |
| `slices[].playwright.project` | Optional; a test project that the slice lists, whose build output holds `playwright.ps1`. `run --install-playwright` installs the browsers when this project is selected. |
| `slices[].playwright.browsers` | The browsers to install, such as `chromium`: lowercase letters, digits, and `-`, starting with a letter. At least one. |
| `slices[].playwright.onlyShell` | Optional, default `false`: install only the headless shell of Chromium, not the full browser. Set it when the slice's tests never run headed. |
| `slices[].service.project` | Optional section. A project of the solution that is not a test project and that no slice lists. `run` starts it with `dotnet run` before the slice's tests and stops it after them; see [Services](#services). |
| `slices[].service.readyFileVariable` | The environment variable, in the service's environment, that names the file where the service writes its value: letters, digits, and `_`, not starting with a digit, and not `GH_TOKEN` or `GITHUB_TOKEN`. |
| `slices[].service.valueVariable` | The environment variable that carries the written value into the slice's test run, and only there. It follows the rules of `readyFileVariable`, differs from it, and differs from the `valueVariable` of every other slice. |
| `slices[].service.readyTimeoutSeconds` | Optional, from 1 to 3600, default 300: how long the service has to write its value, enough for an image pull on a cold runner. |
| `slices[].service.stopTimeoutSeconds` | Optional, from 1 to 3600, default 60: how long the service has to exit after its standard input closes before its process tree is killed. A service that removes containers needs more than the two minutes that the container runtime may take, because it removes them while it exits and a kill interrupts that; a helper that the service detached, such as Aspire's DCP process, can still remove what a killed service left, but that is a safety net and not the plan. |
| `defaultSlice` | Optional; the slice that runs the selected test projects that no slice lists. Without it, every test project of the solution must be in a slice. |
| `openApi[].name` | Optional section. A target name: lowercase letters and digits in words separated by `-`, unique. |
| `openApi[].project` | The project that `dotnet build` builds for the target. |
| `openApi[].generateProperty` | The MSBuild property set to `true` to generate the documents: letters, digits, and `_`, not starting with a digit. |
| `openApi[].refreshProperty` | The MSBuild property set to `true` to generate the documents and refresh their committed copies. |
| `openApi[].preserveEnvironment` | Optional names of parent environment variables, letters, digits, and `_`, not starting with a digit and none repeated, that the target's build keeps when they have a value, in addition to the fixed minimal environment. The list is for non-secret flags: a name containing `TOKEN`, `SECRET`, `PASS`, `AUTH`, `KEY`, `CREDENTIAL`, `PRIVATE`, `CERT`, `CONNECTION`, `SAS`, or `SIGNATURE`, in any case, is an error, as is a name that starts with `LD_` or `DYLD_` or is `DOTNET_STARTUP_HOOKS`. The names cannot replace `PATH` or the OpenAPI generation settings. |
| `coverage.thresholdPercent` | Optional section, required by `coverage`. The least share, from 0 to 100, of the changed executable lines and conditions that the tests cover. |
| `coverage.include` | Path patterns of the source files whose added lines count. At least one. |
| `coverage.exclude` | Path patterns of included files whose added lines do not count, such as generated code. |
| `coverage.symbolDirectories` | Directories whose `bin/Release` directories hold the portable PDBs that `coverage record-symbols` records. At least one. |
| `mutation.strykerVersion` | Optional section, required by `run-mutation-tests`. The exact `dotnet-stryker` package version, such as `4.15.0`: three numbers and an optional prerelease label. |
| `mutation.configFiles` | The Stryker config files that `run-mutation-tests` runs, in order. At least one, none repeated. |

Paths are relative to the root, use `/`, and have no empty, `.`, or `..` segment; they resolve under the root without
following symbolic links. Patterns match the whole repository-relative path with
`FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false)`: `*` matches any characters, including `/`,
and `?` matches one. A test project references `xunit.v3.mtp-v2` or sets `IsTestProject` to `true` in its project file.

### select

Compares `--base` with `HEAD` and writes the validation plan to `--selection` (default `TestResults/selection`). The
changed paths are `git diff --name-only --no-renames -z <base>...HEAD`, after `git merge-base --is-ancestor <base> HEAD`
confirms that the base is an ancestor of `HEAD`; each Git command stops after 30 seconds. Without `--base`, or with an
empty one, one that is not a 40- or 64-character hexadecimal object ID, one that is not an ancestor of `HEAD`, or a
failed or stopped comparison, every test project is selected with the reason. Git never receives a `--base` that is not
an object ID.

Each changed path is classified in order:

1. A path that matches a `fullValidation` pattern selects every test project.
2. A path that matches a `documentation` pattern selects nothing while every tracked build file is inert. A build file
   is a `*.csproj`, `*.props`, or `*.targets` file that `git ls-files` lists. An inert file has no `Exec`, `UsingTask`,
   or `ReadLinesFromFile` element and no `System.IO.File` property function, and imports only `Sdk.props` and
   `Sdk.targets` of `Microsoft.NET.Sdk`, or, in a `Directory.Build.props` or `Directory.Build.targets`, the file of the
   same name above it through `$([MSBuild]::GetPathOfFileAbove('<name>', '$(MSBuildThisFileDirectory)..'))` (or `../`).
   Otherwise the path is classified as any other path.
3. A path that matches a `testPaths` pattern selects the test projects of the first matching rule, even when a solution
   project directory contains it, and no other project.
4. Any other path is owned by the projects that compile it through an explicit `Compile` path and by the solution
   project whose directory most closely contains it. A path without an owner selects every test project.

Selection reads `ProjectReference` and `Compile` items from project files without evaluating MSBuild. A project file that
cannot be read, imports anything other than `Sdk.props` or `Sdk.targets` of `Microsoft.NET.Sdk`, or has a reference or
compile item with a property, an item, a metadata reference, a wildcard, or a list, or that resolves outside the
solution, makes the references uncertain, and a change that needs them selects every test project.

The selected test projects are the test projects among the owning projects and the projects that reference them
directly or transitively, plus `alwaysSelected`. With `requireCoveringTests`, a changed project that no selected test
project depends on or is selects every test project. A slice runs only its selected test projects; the default slice
also runs the selected test projects that no slice lists. A change of no path selects nothing.

The plan directory receives:

- `selection.json`: the revision, the base, `fullValidation`, `buildRequired`, `documentationOnly`, the reasons, the
  selected non-test projects (every solution project for full validation), and every slice with whether it is required
  and its selected test projects, each with the changed paths that selected it (`alwaysSelected` for a configured one;
  none for full validation).
- `<slice>.slnf` for each slice with a selected test project: a solution filter that lists exactly those test projects.
  For full validation, it lists every test project of the slice.
- `build.slnf` when a non-test project is selected: a solution filter of the selected non-test projects; for full
  validation, of every solution project.

A filter names the solution relative to the filter and lists projects relative to the solution, in ordinal order, with
`/` separators. `dotnet restore`, `dotnet build`, and `dotnet test --solution` accept a filter in place of the solution
and load only the listed projects and the projects they reference. Filters that an earlier plan left in the directory
are deleted. Selection reasons go to standard output.

`--github-output <path>` appends these lines to a GitHub Actions output file:

| Output | Value |
| --- | --- |
| `build_required` | `true` when any project is selected. |
| `documentation_only` | `true` when the change selects nothing because it touches only documentation, or nothing. |
| `matrix` | A JSON array of the slices with a selected test project, in configuration order, such as `["fast"]`. |
| `<slice>_required` | `true` when the slice has a selected test project; one line per configured slice. |

### run

```text
dotnet tool run sharedkernel-ci -- run --slice <name> --coverage --install-playwright --playwright-dependencies --github-summary "$GITHUB_STEP_SUMMARY"
```

Runs the selected test projects of the plan in `--selection` (default `TestResults/selection`). Each `--slice` names a
slice; several slices run together, and without `--slice` every required slice runs. The run rejects a plan recorded for
another revision, an unknown slice, and a plan that names a test project outside the checkout or through a symbolic
link. When the slices have no selected test project, standard output says so and nothing is built.

The run writes one solution filter, `tests.slnf`, of the selected test projects and the projects of the slices'
`openApi` targets and services to its execution directory, `execution-<slice>` (several slices join their names with `+`, as in
`execution-fast+system`; without `--slice`, `execution`) in the plan directory, and then:

1. `dotnet restore tests.slnf --locked-mode`;
2. `dotnet build tests.slnf -c <configuration> --no-restore`;
3. for each `openApi` target of the slices, in order, the target's build with its generate property, as `openapi
   generate` runs it, with `-c <configuration>`;
4. with `--install-playwright`, `install-playwright` for each slice whose `playwright.project` is selected, with its
   browsers, `--configuration <configuration>`, `--only-shell` when `playwright.onlyShell` is set, and, with `--playwright-dependencies`, `--with-dependencies`;
5. for each slice with a `service`, in configuration order, the service, as [Services](#services) describes;
6. one `dotnet test --solution tests.slnf -c <configuration> --no-build --no-restore --results-directory results`, with
   native MTP module scheduling and no module-parallelism cap, and after it the stop of the services in reverse order.

`--configuration` defaults to `Release`. `--coverage` adds `--results-directory-layout per-module -- --coverlet
--coverlet-output-format cobertura`, so each test project writes its Cobertura report under
`results/<project>/<target-framework>_<runtime>`, and the run fails when a selected test project produced no
`*.cobertura*.xml` report. The test projects need the `coverlet.MTP` extension. Results that an earlier run left are
deleted before the tests run.

Each `--exclude-trait <name=value>` leaves out the tests with that xUnit trait: the run passes `--filter-not-trait
<name=value>` to the test run, after `--` and after the coverage arguments. Repeat the option for several traits. The
name and value are non-empty and contain no whitespace, and the name does not start with `-`. A consuming repository
uses this to run, in an environment that lacks an external dependency, every test except the ones that need it.

Each phase writes its standard output to `<phase>.log`, or its failure message when it fails, and to standard error.
Whether the run succeeds, fails, or is canceled, configuration and plan loading included, the execution directory
receives `phase-timings.tsv` (phase, start, duration in seconds, and status, then a `total` line), `manifest.json` (the
revision, the status, the slices, the configuration, the options, the test projects, and the phases with their logs),
and `diagnostics.txt` (the same facts, the escaped failure message, and every file in the directory). `--github-summary
<path>` appends a table of the phases to a GitHub Actions step summary. A failed phase's standard output and standard
error appear in its failure message on standard error.

#### Services

A slice's `service` is a process that all of the slice's test projects share, such as one database server, and that
only `run` can start once for them. The configuration names its project, the variable that tells it where to write its
value, and the variable that hands the value to the tests. The protocol is:

1. The service project is in `tests.slnf`, so restore and build cover it. After the build, the OpenAPI generation, and
   the Playwright install, `run` starts it from the root with `dotnet run --project <project> -c <configuration>
   --no-build --no-restore --no-launch-profile`. `dotnet run` passes its redirected standard input to the application,
   so closing the input reaches the service as end-of-file, and the application's exit code is the exit code of
   `dotnet run`.
2. The service's environment is the tool's, without `GH_TOKEN` and `GITHUB_TOKEN`, without `valueVariable`, and with
   `readyFileVariable` set to the absolute path of a file in a fresh temporary directory. Only the current user can
   enter that directory (mode 0700 on Unix), and it lies outside the execution directory, which CI uploads as an
   artifact. Standard input is a pipe that the tool holds open. Standard output and standard error stream to
   `service-<slice>.stdout.log` and `service-<slice>.stderr.log` in the execution directory, where the `service-<slice>`
   phase log names them.
3. When the server accepts connections, the service writes its value to the ready file as one UTF-8 line without a byte
   order mark: it writes `<file>.tmp` and moves it over the file, so the file never exists before the value is whole.
   The run polls for the file. The first line, trimmed, is the value, and an empty file or line is not a value yet; the
   file is at most 4 KiB and the line free of control characters. As soon as the run has read the value it deletes the
   file, so a run that is cut short leaves an empty directory and no credential. If the service exits first, the run
   fails with the exit code and the log paths. If `readyTimeoutSeconds` pass first, the run fails too.
4. The test phase runs with the value in `valueVariable`, in the environment of that child process only. The value is
   never an argument, and no earlier phase and no other process receives it. The run replaces the value in the test
   output and in the failure message of the test phase with `[redacted]`; it cannot know forms that the tests derive from
   the value, so tests must not print connection strings.
5. After the test phase, and after a failure or a cancellation of any phase, the run closes the service's input. The
   service reads end-of-file as the request to release what it started and exit with code 0. The run waits up to
   `stopTimeoutSeconds` and only then kills the service's process tree, because the service releases what it started
   while it exits and a kill interrupts that. A helper that the service detached, such as Aspire's DCP process, outlives
   the kill and removes what a killed service left within seconds, but that is a safety net and not the plan. The run
   then finishes the logs and deletes the temporary directory.

A service that was killed, exited with a non-zero code, or left its directory fails a run that would otherwise pass. It
never replaces an earlier failure: the first failure is the one that the run reports, and `diagnostics.txt` lists both.
If the tool dies, the service still sees end-of-file, because the pipe closes with the tool. Each service adds a
`service-<slice>` phase, which starts it and waits for its value, and a `service-stop-<slice>` phase to the evidence.
A slice without a `service` runs as before.

### openapi

```text
sharedkernel-ci openapi generate [--target <name>]... [--root <path>]
sharedkernel-ci openapi refresh [--target <name>]... [--root <path>]
```

Builds the `openApi` targets of the configuration, each named `--target` in order, or every target in configuration
order: `dotnet build --no-restore <project> -p:<generateProperty>=true`, or `<refreshProperty>` for `refresh`. Each
build runs through the dotnet host that runs the tool, from the root, with an environment that keeps only `HOME` and
`TMPDIR` (on Windows `TEMP`, `TMP`, `USERPROFILE`, `APPDATA`, `LOCALAPPDATA`, `SystemRoot`, and `ComSpec`),
`NUGET_PACKAGES` when it is the repository's `.nuget/packages`, and a `PATH` of the dotnet host directory and the system
directories, plus the variables that the target's `preserveEnvironment` lists. It sets `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` to `OpenApiGeneration`, the host environment
that `PandaBear.OpenApi.OpenApiGenerationMode` recognizes, turns off telemetry and the first-run experience, and
disables OpenTelemetry export. Credentials, connection strings, proxies, and startup hooks of the parent environment do
not reach the build. Restore the projects first. A configuration without `openApi` targets fails the command.

### install-playwright

```text
sharedkernel-ci install-playwright --project <path> [--browser <name>]... [--with-dependencies] [--only-shell] [--configuration <name>] [--root <path>]
```

Evaluates the project's `OutputPath` with `dotnet msbuild <project> -getProperty:OutputPath`, with
`-p:Configuration=<name>` when `--configuration` is given, normalizes its `\` and `/` separators, resolves it from the
project directory, and runs `pwsh <output>/playwright.ps1 install`, with `--with-deps` for `--with-dependencies`, `--only-shell` for `--only-shell`, and
the browsers. Without `--browser`, Playwright installs its default browsers. The tool writes each line of the installer to standard error with its UTC time as it arrives, then reports the
duration of the evaluation and the installer. Build the project first; the command fails
when the output has no `playwright.ps1`.

### coverage

```text
dotnet tool run sharedkernel-ci -- coverage record-symbols
dotnet tool run sharedkernel-ci -- coverage verify --report artifacts --base "$BASE_SHA" --symbols TestResults/symbol-evidence.json
```

`coverage record-symbols` runs after a Release build. It writes `HEAD` and the SHA-256 hash of every `*.pdb` in a
`bin/Release` directory under the `coverage.symbolDirectories` to `--output` (default
`TestResults/symbol-evidence.json`), and fails when there is none.

`coverage verify` checks, in order:

1. the symbol evidence of `--symbols`: it records `HEAD`, and each file it names is a `bin/Release` `.pdb` under a
   symbol directory, reached without a symbolic link, whose content has the recorded hash;
2. the lines added since `--base`, a full hexadecimal Git object ID, from `git -c core.quotePath=false diff
   --no-ext-diff --no-textconv --no-color --src-prefix=a/ --dst-prefix=b/ --unified=0 --inter-hunk-context=0 --text
   --find-renames <base>...HEAD --`, so repository Git configuration and attributes cannot hide, convert, or move
   changed lines; a path that Git quotes fails the check;
3. the Cobertura reports of each `--report`: a report file, or a directory searched recursively for
   `*.cobertura*.xml`, such as the `results` directory of `run --coverage`. A report with a document type declaration
   is refused. A line is covered when any report hits it.

Only added lines of files that match `coverage.include` and no `coverage.exclude` count: each line that a report lists
counts once, plus its conditions. A changed file that no report lists passes only when a recorded portable PDB names it
with the SHA-256 hash of its content and maps no executable code to it, as for a file of interfaces; otherwise the
check fails. The check passes when nothing counts or the covered share reaches `coverage.thresholdPercent`, and
standard output gives the counts.

Whether the check passes, fails, or is canceled, `--output` (default `TestResults/coverage-gate.json`) receives
`passed`, `thresholdPercent`, `covered`, `total`, `revision`, `parents` (both parents of a pull request merge
checkout), `base`, `configurationSha256` (the hash of `.config/sharedkernel-ci.json`), the `reports` read, and the
`error` of a failed check. For a pull request merge checkout, pass the merge commit's first parent as `--base`.

### stage-candidate

```text
dotnet tool run sharedkernel-ci -- stage-candidate --pin .config/sharedkernel-candidate.json --library-root <sharedkernel-checkout> --output .local/sharedkernel-feed --repository danigutsch/PandaBear.SharedKernel
dotnet tool run sharedkernel-ci -- stage-candidate --pin .config/sharedkernel-candidate.json --library-root <sharedkernel-checkout> --output .local/sharedkernel-feed --feed <packages>
```

Stages the packages of a pinned candidate in a local feed. `--pin` names the candidate manifest that the consumer
pins, `--library-root` a SharedKernel checkout, and `--output` the local feed. Exactly one source is required:
`--repository`, the GitHub `owner/name` whose `v<packageVersion>` release holds the packages, or `--feed`, a directory
that holds them. `--root` names the consumer repository.

Before it sends a request or changes a file, the command checks the pin: strict JSON of at most 1 MiB, without
comments, trailing commas, or a repeated property; `libraryRevision` of 40 lowercase hexadecimal characters;
`libraryDirty` set to `false`; `packageVersion` of letters, digits, `.`, and `-`; and at least one `packageSha256`
entry, each a 64-character lowercase hexadecimal hash of a package named
`PandaBear.SharedKernel.<id>.<packageVersion>.nupkg`, with no two names that differ only in case. Evidence properties,
such as `linuxPackageConsumption`, are not checked. It then checks, in order, that:

1. `sdk.version` and `sdk.rollForward` match in `<root>/global.json` and `<library-root>/global.json`, because the
   consumer builds the library's tools with its own SDK;
2. `--library-root` is the top level of a Git work tree;
3. its `HEAD` is `libraryRevision`;
4. `git status --porcelain` reports nothing.

With `--repository`, the command downloads the release's `candidate.json`, at most 1 MiB, on every run and requires
its `libraryRevision`, `libraryDirty`, `packageVersion`, and `packageSha256` to equal the pin's, so the cached
packages of a deleted or re-created release are refused; evidence properties may differ. It then downloads only the
packages missing from `--output`, at most 64 MiB each and 256 MiB in all, counted while they stream. With `--feed`, it
copies the missing packages from the directory and sends no request.

Packages land in a staging directory beside `--output`, on the same volume, named `.<output name>.stage-<id>`. They
are verified against the pinned SHA-256 hashes there and only then move into `--output`. A package already in
`--output` is kept when its hash matches and refused otherwise; the command never overwrites it, and a concurrent run
with the same pin may move the same package first. The pin's exact bytes are written last, through a temporary file,
as `<output>/candidate.json`. Packages of other versions in `--output` stay. The staging directory is deleted when the
command ends; one older than a day, which an interrupted run left behind, is deleted when the next run starts, while a
concurrent run's newer one stays. A staging directory that cannot be deleted stays for a later run and does not fail
the command. Standard output reports how many packages were added and how many were already present.

### verify-commit-message

Checks that a message header is a Conventional Commit header of at most 100 characters with no surrounding whitespace,
and that every `BREAKING CHANGE` or `BREAKING-CHANGE` footer has a description. Pass exactly one source:

- `--event <path>` reads a GitHub `pull_request` event file: the title is the header and the body holds the footers.
  The PR metadata workflow runs it separately from source validation.
- `--file <path>` reads a commit message file as UTF-8, as git passes it to a `commit-msg` hook. Trailing whitespace
  is ignored, the `# ------------------------ >8 ------------------------` line that `git commit -v` writes ends the
  message, and lines that start with `#` after optional whitespace are comments. The first remaining non-blank line
  is the header. The check skips the `fixup! `, `amend! `, and `squash! ` prefixes that `git commit --fixup` and
  `--squash` write for `git rebase --autosquash`, so the subject after them must pass. A header that starts with
  `Merge `, `Revert `, or `Reapply `, as git writes for merge commits, reverts, and reverts of reverts, passes.

Errors are fixed text and never repeat the title or message.

To check each local commit, save this hook as the file that `git rev-parse --git-path hooks/commit-msg` prints and
make it executable. That path follows `core.hooksPath`. When only the global Git configuration sets it, every
repository shares the path, so first set `core.hooksPath` in this repository to a directory of its own. Git runs hooks
from the top of the working tree, where `dotnet tool run` finds the tool manifest.

```sh
#!/bin/sh
exec dotnet tool run sharedkernel-ci -- verify-commit-message --file "$1"
```

### run-mutation-tests

```text
dotnet tool run sharedkernel-ci -- run-mutation-tests
```

Runs Stryker.NET with each `mutation.configFiles` entry, in order, and stops at the first run that fails. Every config
file must exist before anything runs. Unless `<root>/TestResults/tools/dotnet-stryker/<strykerVersion>` already holds
`dotnet-stryker`, the command first runs `dotnet tool install dotnet-stryker --version <strykerVersion> --tool-path
<that directory>` from the root. The tool stays out of the tool manifest, so other tool restores never download it, and
later runs reuse the installed version. Each config then runs as `<tool path>/dotnet-stryker --config-file <file name>`
from the config file's directory, where Stryker writes its `StrykerOutput` reports. Standard output receives a `==>
<config file>` line before each run and the run's output when it ends; a failed run's output is in its failure message
on standard error.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | Invalid command line, cancellation, or an unexpected error. |
| 2 | The configuration is invalid, or a run, generation, installation, coverage check, staging, the commit message check, or a mutation run failed; the reason is on standard error. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within 90 seconds. |

Ctrl+C or SIGTERM stops the running `git`, `dotnet`, `pwsh`, or `dotnet-stryker` process tree, then stops the services of
a `run` through their standard input, finishes the run's evidence, and exits with 1. A service that removes its container
needs about ten seconds for that. When this cleanup exceeds 90 seconds the tool exits with 130 or 143 instead, without
the rest of the evidence, and the closed input still tells a service to stop. A second Ctrl+C or SIGTERM does not shorten
the wait. A process that fails after the tool has observed Ctrl+C or SIGTERM counts as the cancellation. Error messages
on standard error escape control and format characters line by line, so text from files, processes, and the network
cannot drive the terminal.

## Security

The tool starts only `git`, `dotnet`, and `pwsh`, found in the absolute directories of `PATH` and never in the working
directory, or, for OpenAPI generation, the dotnet host that runs the tool, or the `dotnet-stryker` that
`run-mutation-tests` installs under `TestResults/tools`, and passes arguments as a list. A slice's service is a `dotnet run`
of the solution project that the configuration names, started without a GitHub token in its environment. Slice,
target, browser, configuration, and MSBuild property values are restricted to names, so none can become an option or
another property. `select` and `coverage verify` pass `--base` to Git only when it is a hexadecimal object ID, so Git
cannot read it as an option, and escapes control characters in the changed paths that its reasons repeat. `verify-commit-message` starts no process. No credentials are needed. `stage-candidate` runs
Git without the inherited `GIT_*` variables, which could point it at another repository, and stops Git after a minute.

A service's value can be a credential, such as a connection string with a password. The service writes it to a ready file in
a temporary directory that only the current user can enter and that lies outside the execution directory. The tool
deletes the file as soon as it has read the value and the directory when the service stops, also when a signal cancels
the run. The value reaches the test phase through the environment of the child
process and never through the arguments that failure messages print. Neither the value nor the ready file appears in the
evidence, the phase logs, or the messages, and the service's own output goes to log files that a service must keep free
of it.

`stage-candidate --repository` sends unauthenticated HTTPS `GET` requests to
`https://github.com/<owner>/<name>/releases/download/v<version>/<asset>`, which GitHub redirects to its release-asset
host, `release-assets.githubusercontent.com`. It follows at most five redirects and refuses one that is not HTTPS,
sends a `User-Agent`, and never sends an `Authorization` header or a token. Each request, with its redirects and body,
times out after five minutes. A 404 or another client error fails at once; 408, 429, and 5xx answers are retried, four
attempts in all, after the `Retry-After` delay capped at 60 seconds, or after one, two, and four seconds without it.
Integrity rests on the pinned SHA-256 hashes, not on the transport.
