# SharedKernel CI tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-ci` selects the SharedKernel solution projects affected by a change, restores and builds them as one
graph, runs complete MTP test projects, and verifies Conventional Commit messages in pull request events and git commit
message files.

## Prerequisites

Git and the .NET SDK pinned in `global.json`. `select` and `validate` read `SharedKernel.slnx` in a Git checkout;
`verify-commit-message` reads only its event or message file.

## Installation

Install a validated candidate as a local tool from the feed that holds it, such as the folder that
`sharedkernel-version stage-candidate` copies a candidate to. Add the feed to `NuGet.Config` and map
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
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- select --base <revision>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- validate
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- verify-commit-message --event <event.json>
```

Package ID: `PandaBear.SharedKernel.CI.Tool`. C# namespace: `PandaBear.CI.Tool`. Assembly/project identity:
`SharedKernel.CI.Tool`. Source is public; registry publication remains deferred, and nothing here implies
availability on nuget.org.

## Usage

`--help` lists the options of each command. `--root` sets the repository root (default: the current directory);
relative `--selection` and `--github-output` paths resolve from it, and relative `--event` and `--file` paths resolve
from the current directory. A command line that starts with `[diagram]` prints how it parses instead of running; an
invalid command line exits 1.

### select

Writes the validation plan to `--selection` (default `TestResults/selection`): `revision.txt`, `build-projects.txt`,
`test-projects.txt`, `selection-reasons.txt`, `selection.json`, and `candidate-required.txt`. Selection reads the live
solution and adds reverse transitive dependents, whole test projects, and the package-consumption dependencies in
`tools/SharedKernel.CI.Tool/package-consumption-edges.json` under the root, when that file exists. Without `--base`,
or when history or a dependency is uncertain, every project is selected. Selection reasons go to standard output.

Arguments that start with `@` name response files, so write `--base HEAD@{u}` instead of `--base @{u}`.

`--github-output <path>` appends `candidate_required`, `hosted_required`, and `hosted_matrix` (the selected Aspire
test projects) to a GitHub Actions output file.

### validate

Restores and builds the planned projects as one temporary solution graph, then runs complete test projects with
native MTP module scheduling and no module-parallelism cap. `--test-group` selects `all` (default), `ordinary`
(everything except hosted Aspire test projects), or one hosted test project path. Validation rejects a plan recorded
for another revision or naming projects outside the live solution, and keeps timings under `execution-*` in the plan
directory. When a `dotnet` restore, build, or test step succeeds, its standard output goes to standard error after the
step ends, and its standard error is not shown. A failed step's standard output and standard error appear in its
failure message on standard error. Standard output stays empty unless the plan selects no projects for the group.
Package candidates are created with `SharedKernel.Versioning.Tool`; see
[package development](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/package-development.md).

### verify-commit-message

Checks that a message header is a Conventional Commit header of at most 100 characters with no surrounding whitespace,
and that every `BREAKING CHANGE` or `BREAKING-CHANGE` footer has a description. Pass exactly one source:

- `--event <path>` reads a GitHub `pull_request` event file: the title is the header and the body holds the footers.
  The PR metadata workflow runs it separately from source validation.
- `--file <path>` reads a commit message file as UTF-8, as git passes it to a `commit-msg` hook. Trailing whitespace
  is ignored, the `# ------------------------ >8 ------------------------` line that `git commit -v` writes ends the
  message, and lines that start with `#` after optional whitespace are comments. The first remaining non-blank line
  is the header. A message whose header starts with `Merge `, `Revert `, or `Reapply `, as git writes for merge
  commits, reverts, and reverts of reverts, passes.

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
| 2 | Selection, validation, or the commit message check failed; the reason is on standard error. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within two seconds. |

Ctrl+C or SIGTERM stops the running `git` or `dotnet` process tree. A process that fails after the tool has observed
Ctrl+C or SIGTERM counts as the cancellation.

## Security

The tool starts only `git` and `dotnet` and passes arguments as a list. `--base` rejects empty values and values
starting with `-`, so Git cannot read them as options. `verify-commit-message` starts no process. No credentials are
needed.
