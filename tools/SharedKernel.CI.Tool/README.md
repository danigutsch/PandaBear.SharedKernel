# SharedKernel CI tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-ci` selects the SharedKernel solution projects affected by a change, restores and builds them as one
graph, runs complete MTP test projects, stages pinned package candidates in a local feed, and verifies pull request
titles.

## Prerequisites

Git and the .NET SDK pinned in `global.json`. `select` and `validate` read `SharedKernel.slnx` in a Git checkout;
`stage-candidate` reads a clean SharedKernel checkout and, with `--repository`, needs HTTPS access to `github.com` and
`release-assets.githubusercontent.com`; `verify-pr-title` reads only its event file.

## Installation

Install a validated candidate as a local tool from the feed that holds it, such as the folder that
`sharedkernel-ci stage-candidate` stages a candidate in. Add the feed to `NuGet.Config` and map
`PandaBear.SharedKernel.*` to it. Map every other package, for example `*`, to the existing source, because
a package without a mapping does not restore once source mapping is enabled. With the SDK pinned in
`global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.CI.Tool --version <validated-candidate-version>
dotnet tool run sharedkernel-ci -- verify-pr-title --event <event.json>
```

.NET tools run in full trust. Install only from trusted manifests and package sources.

The tool also builds from a cone sparse checkout of `tools/SharedKernel.CI.Tool` and `tools/Shared`, for jobs that
run before a candidate exists. Its only package dependency is System.CommandLine. In this repository, run the project
without installing it:

```sh
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- select --base <revision>
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- validate
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- stage-candidate --pin <candidate.json> --library-root . --output <feed> --repository danigutsch/PandaBear.SharedKernel
dotnet run --project tools/SharedKernel.CI.Tool -c Release -- verify-pr-title --event <event.json>
```

Package ID: `PandaBear.SharedKernel.CI.Tool`. C# namespace: `PandaBear.CI.Tool`. Assembly/project identity:
`SharedKernel.CI.Tool`. Source is public; registry publication remains deferred, and nothing here implies
availability on nuget.org.

## Usage

`--help` lists the options of each command. `--root` sets the repository root (default: the current directory);
the other relative paths of `select`, `validate`, and `stage-candidate` resolve from it. A command line that starts
with `[diagram]` prints how it parses instead of running; an invalid command line exits 1.

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
concurrent run's newer one stays. Standard output reports how many packages were added and how many were already
present.

### verify-pr-title

Checks that the `pull_request` title in a GitHub event file is a Conventional Commit header of at most 100 characters
and that every `BREAKING CHANGE` footer has a description. The PR metadata workflow runs it separately from source
validation.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | Invalid command line, cancellation, or an unexpected error. |
| 2 | Selection, validation, staging, or the pull request check failed; the reason is on standard error. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within two seconds. |

Ctrl+C or SIGTERM stops the running `git` or `dotnet` process tree. A process that fails after the tool has observed
Ctrl+C or SIGTERM counts as the cancellation. Error messages on standard error escape control and format characters
line by line, so text from files, processes, and the network cannot drive the terminal.

## Security

The tool starts only `git` and `dotnet`, found in the absolute directories of `PATH` and never in the working
directory, and passes arguments as a list. `--base` rejects empty values and values starting with `-`, so Git cannot
read them as options. No credentials are needed. `stage-candidate` runs Git without the inherited `GIT_*` variables,
which could point it at another repository, and stops Git after a minute.

`stage-candidate --repository` sends unauthenticated HTTPS `GET` requests to
`https://github.com/<owner>/<name>/releases/download/v<version>/<asset>`, which GitHub redirects to its release-asset
host, `release-assets.githubusercontent.com`. It follows at most five redirects and refuses one that is not HTTPS,
sends a `User-Agent`, and never sends an `Authorization` header or a token. Each request, with its redirects and body,
times out after five minutes. A 404 or another client error fails at once; 408, 429, and 5xx answers are retried, four
attempts in all, after the `Retry-After` delay capped at 60 seconds, or after one, two, and four seconds without it.
Integrity rests on the pinned SHA-256 hashes, not on the transport.
