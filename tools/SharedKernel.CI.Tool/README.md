# SharedKernel CI tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-ci` selects the test projects affected by a change, as the repository's `.config/sharedkernel-ci.json`
configures, writes a solution filter for each CI test slice, restores, builds, and tests a slice's filter, stages pinned
package candidates in a local feed, and verifies Conventional Commit messages in pull request events and git commit
message files.

## Prerequisites

Git and the .NET SDK pinned in `global.json`. `select` reads `.config/sharedkernel-ci.json` and the solution it names in a
Git checkout, and `validate` reads the plan that `select` wrote;
`stage-candidate` reads a clean SharedKernel checkout and, with `--repository`, needs HTTPS access to `github.com` and
`release-assets.githubusercontent.com`; `verify-commit-message` reads only its event or message file.

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
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- validate --slice <name>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- stage-candidate --pin <candidate.json> --library-root . --output <feed> --repository danigutsch/PandaBear.SharedKernel
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- verify-commit-message --event <event.json>
```

Package ID: `PandaBear.SharedKernel.CI.Tool`. C# namespace: `PandaBear.CI.Tool`. Assembly/project identity:
`SharedKernel.CI.Tool`. Source is public; registry publication remains deferred, and nothing here implies
availability on nuget.org.

## Usage

`--help` lists the options of each command. `--root` sets the repository root (default: the current directory);
the other relative paths of `select`, `validate`, and `stage-candidate` resolve from it, and relative `--event` and
`--file` paths resolve from the current directory. A command line that starts with `[diagram]` prints how it parses
instead of running; an invalid command line exits 1.

### Configuration

`select` reads `<root>/.config/sharedkernel-ci.json`: strict JSON of at most 1 MiB with camelCase property names that
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
    "requireCoveringTests": true
  },
  "slices": [
    { "name": "fast", "projects": [] },
    { "name": "system", "projects": ["tests/System.Tests/System.Tests.csproj"], "openApi": ["admin"], "playwright": true }
  ],
  "defaultSlice": "fast"
}
```

| Property | Meaning |
| --- | --- |
| `version` | `1`. |
| `solution` | The `.slnx` whose projects are selected, relative to the root. |
| `selection.fullValidation` | Path patterns whose change selects every test project. |
| `selection.documentation` | Path patterns whose change selects nothing while every tracked build file is inert. |
| `selection.alwaysSelected` | Test projects that run whenever a change selects any project. |
| `selection.requireCoveringTests` | Whether a changed project that no selected test project depends on selects every test project. |
| `slices[].name` | Lowercase letters and digits in words separated by `-`, other than `build`. It names the matrix entry, the filter, and the `<name>_required` output. |
| `slices[].projects` | The slice's test projects. Each is a test project of the solution, listed in one slice. Only the default slice may list none. |
| `slices[].openApi` | Optional OpenAPI target names that the slice generates before its tests run. |
| `slices[].playwright` | Optional; whether the slice installs Playwright browsers before its tests run. Default `false`. |
| `defaultSlice` | Optional; the slice that runs the selected test projects that no slice lists. Without it, every test project of the solution must be in a slice. |

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
3. Any other path is owned by the projects that compile it through an explicit `Compile` path and by the solution
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

### validate

Restores, builds, and tests the solution filter of one slice: `dotnet restore <slice>.slnf --locked-mode`, `dotnet build
<slice>.slnf -c Release --no-restore`, and `dotnet test --solution <slice>.slnf -c Release --no-build --no-restore`, with
native MTP module scheduling and no module-parallelism cap. `--slice` names the slice. Validation rejects a plan
recorded for another revision, an unknown slice, and a required slice without its filter, and keeps timings under
`execution-<slice>` in the plan directory. When a `dotnet` restore, build, or test step succeeds, its standard output
goes to standard error after the step ends, and its standard error is not shown. A failed step's standard output and
standard error appear in its failure message on standard error. Standard output stays empty unless the slice has no
selected test project. Package candidates are created with `SharedKernel.Versioning.Tool`; see
[package development](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/package-development.md).

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

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | Invalid command line, cancellation, or an unexpected error. |
| 2 | The configuration is invalid, or validation, staging, or the commit message check failed; the reason is on standard error. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within two seconds. |

Ctrl+C or SIGTERM stops the running `git` or `dotnet` process tree. A process that fails after the tool has observed
Ctrl+C or SIGTERM counts as the cancellation. Error messages on standard error escape control and format characters
line by line, so text from files, processes, and the network cannot drive the terminal.

## Security

The tool starts only `git` and `dotnet`, found in the absolute directories of `PATH` and never in the working
directory, and passes arguments as a list. `select` passes `--base` to Git only when it is a hexadecimal object ID, so Git
cannot read it as an option, and escapes control characters in the changed paths that its reasons repeat. `verify-commit-message` starts no process. No credentials are needed. `stage-candidate` runs
Git without the inherited `GIT_*` variables, which could point it at another repository, and stops Git after a minute.

`stage-candidate --repository` sends unauthenticated HTTPS `GET` requests to
`https://github.com/<owner>/<name>/releases/download/v<version>/<asset>`, which GitHub redirects to its release-asset
host, `release-assets.githubusercontent.com`. It follows at most five redirects and refuses one that is not HTTPS,
sends a `User-Agent`, and never sends an `Authorization` header or a token. Each request, with its redirects and body,
times out after five minutes. A 404 or another client error fails at once; 408, 429, and 5xx answers are retried, four
attempts in all, after the `Retry-After` delay capped at 60 seconds, or after one, two, and four seconds without it.
Integrity rests on the pinned SHA-256 hashes, not on the transport.
