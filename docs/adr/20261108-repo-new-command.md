# Repo new command

Date: 2026-11-08. Status: accepted.

`dotnet repo baseline apply` keeps the baseline files of an existing repository, but a repository that does not exist yet has no `.config/sharedkernel-repo.json` to apply. `dotnet repo new` starts a repository: it chooses a language and features, writes the matching baseline files, records them in a consumer configuration, and initializes Git. It extends [the baseline tool](20261028-repo-baseline-tool.md); it does not replace it and is not the deferred `forge` command group.

## Decision

`new` builds a baseline configuration in memory from the chosen language and features, plans it with the baseline planner, and writes it with the baseline writer. After `new`, `baseline verify` and `baseline apply` work on the repository without further setup.

- **Command**. Run `dotnet repo new` with optional `--root`, `--name`, `--branch`, `--language`, and feature options.
  The root defaults to the current directory and the name to its last segment. Use `--force` to create the repository.
- **Language:** `none` (default) or `dotnet`. A language only selects content for a feature (the `dotnet` language selects the .NET `.gitignore`); it never adds a file by itself.
- **Features**. Standard features include `readme`, `gitignore`, `gitattributes`, `editorconfig`, `hooks`, and `git-config`.
  The `hooks` feature uses a tracked `pre-commit` hook and `core.hooksPath`. Signing and the extra hooks are opt-in.
  Repeat `--feature` for an exact list or repeat `--with` to add features to the language defaults. The options are mutually
  exclusive. Unknown IDs produce an error listing the valid choices. Signing requires a key from the caller.
  The write-once features are `agents` and, for `dotnet`, `global-json`, `nuget-config`, and `build-props`. An existing file
  conflicts with its seed. `global-json` and `build-props` need `--sdk-version`. The test runner setting in `global-json`
  applies from SDK 10 onward.
- **Optional hooks**. `pre-push-hook` checks whitespace at each updated ref's tip, skips deletions, and clears inherited Git
  environment variables. `commit-msg-hook` verifies the message with the repository's restored `ci` tool. Both use the tracked
  hooks directory and the existing baseline activation.
- **Written files:** the selected baseline files with the managed-file marker, `.config/sharedkernel-repo.json` holding the same baseline section, and, unless a Git repository already exists at the root, `git init` with the chosen branch followed by the Git settings.
- **Atomicity:** the files and the configuration are written in one rollback batch, as `baseline apply` does. A conflict anywhere (an existing unmanaged file, a different Git setting, an existing configuration file) writes nothing and fails, in a preview too. A preview that would fail under `--force` never exits 0.

## Generic by construction

The catalog embeds neutral content. The caller supplies `LICENSE`, `SECURITY.md`, and `CODEOWNERS` through the
optional `--license-source`, `--security-source`, and `--codeowners-source` flags. These sources may use
`@@owner@@` and `@@copyrightHolder@@`, supplied with `--owner` and `--copyright-holder`, as well as the existing
`@@repositoryName@@` and `@@defaultBranch@@`. The scaffold validates and renders every supplied source before writes,
then creates the policy files once with the rest of the batch. The generated baseline configuration omits the source
paths and caller values. Consumers supply CI workflows separately.

## Value objects

A language and a feature are sealed records with a private constructor and a `TryParse` that accepts only catalog IDs, so an invalid value cannot reach the planner and `default` cannot exist. The repository name and branch reuse the baseline configuration validation, so one rule covers `new` and `baseline`.

## Comparing write-once files

`dotnet repo seed-drift` optionally compares selected write-once files with the current catalog. The caller
selects features explicitly and supplies the repository name or SDK version needed to render them. Seed selections and
SDK inputs are supplied at scaffold time, and consumers can customize the resulting files. The read-only command reports
paths and statuses while keeping file contents private. Use `baseline verify` to check managed files.

## Deferred

- A failure of `git init` or of a Git setting after the file batch leaves the files in place; remove the directory and rerun, or run `git init` and `baseline apply` in it.
- Per-forge files (pull request template path, issue forms, CI workflows) need a forge choice; this follows the deferred `forge` group.
- Further languages.
