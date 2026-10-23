# Repository validation tool

`SharedKernel.Repository.Tool` is a repository-only executable and is not packaged. It selects the solution projects
affected by a change, restores and builds them as one graph, runs complete MTP test projects, and verifies pull request
titles. Run it from a Git checkout with the SDK pinned in `global.json`.

## Usage

```sh
dotnet run --project tools/SharedKernel.Repository.Tool -c Release -- select --base <revision>
dotnet run --project tools/SharedKernel.Repository.Tool -c Release -- validate
dotnet run --project tools/SharedKernel.Repository.Tool -c Release -- verify-pr-title --event <event.json>
```

`--help` lists the options of each command. `--root` sets the repository root (default: the current directory);
relative `--selection` and `--github-output` paths resolve from it.

### select

Writes the validation plan to `--selection` (default `TestResults/selection`): `revision.txt`, `build-projects.txt`,
`test-projects.txt`, `selection-reasons.txt`, `selection.json`, and `candidate-required.txt`. Selection reads the live
solution and adds reverse transitive dependents, whole test projects, and the package-consumption dependencies in
`package-consumption-edges.json`. Without `--base`, or when history or a dependency is uncertain, every project is
selected. Selection reasons go to standard output.

Arguments that start with `@` name response files, so write `--base HEAD@{u}` instead of `--base @{u}`.

`--github-output <path>` appends `candidate_required`, `hosted_required`, and `hosted_matrix` (the selected Aspire
test projects) to a GitHub Actions output file.

### validate

Restores and builds the planned projects as one temporary solution graph, then runs complete test projects with
native MTP module scheduling and no module-parallelism cap. `--test-group` selects `all` (default), `ordinary`
(everything except hosted Aspire test projects), or one hosted test project path. Validation rejects a plan recorded
for another revision or naming projects outside the live solution, and keeps timings under `execution-*` in the plan
directory. Package candidates are created with `SharedKernel.Versioning.Tool`; see
[package development](../../docs/package-development.md).

### verify-pr-title

Checks that the `pull_request` title in a GitHub event file is a Conventional Commit header of at most 100 characters
and that every `BREAKING CHANGE` footer has a description. The PR metadata workflow runs it separately from source
validation.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | Invalid command line, cancellation, or an unexpected error. |
| 2 | Selection, validation, or the pull request check failed; the reason is on standard error. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or cleanup did not finish within two seconds. |

Ctrl+C or SIGTERM stops the running `git` or `dotnet` process tree.

## Security

The tool starts only `git` and `dotnet` and passes arguments as a list. `--base` rejects empty values and values
starting with `-`, so Git cannot read them as options. No credentials are needed.
