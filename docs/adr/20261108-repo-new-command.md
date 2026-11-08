# Repo new command

Date: 2026-11-08. Status: accepted.

`sharedkernel-repo baseline apply` keeps the baseline files of an existing repository, but a repository that does not exist yet has no `.config/sharedkernel-repo.json` to apply. `sharedkernel-repo new` starts a repository: it chooses a language and features, writes the matching baseline files, records them in a consumer configuration, and initializes Git. It extends [the baseline tool](20261028-repo-baseline-tool.md); it does not replace it and is not the deferred `forge` command group.

## Decision

`new` builds a baseline configuration in memory from the chosen language and features, plans it with the baseline planner, and writes it with the baseline writer. After `new`, `baseline verify` and `baseline apply` work on the repository without further setup.

- **Command:** `sharedkernel-repo new [--root <dir>] [--name <name>] [--branch <branch>] [--language <id>] [--feature <id>]... [--force]`. `--root` is the directory to create; it defaults to the current directory, and `--name` defaults to its last segment. Like every write command it previews unless `--force` is given.
- **Language:** `none` (default) or `dotnet`. A language only selects content for a feature (the `dotnet` language selects the .NET `.gitignore`); it never adds a file by itself.
- **Features:** `readme`, `gitignore`, `gitattributes`, `editorconfig`, `hooks` (a tracked `pre-commit` hook and `core.hooksPath`), `git-config` (`init.defaultBranch`, `pull.ff`, `fetch.prune`), and `signed-commits` (`commit.gpgsign`, `tag.gpgsign`, `gpg.format=ssh`). Four features are written once and not managed: `agents`, and with `--language dotnet` `global-json`, `nuget-config`, and `build-props`. They exist because JSON and XML cannot carry the marker and because the owner edits them; `new` never replaces one, and an existing file is a conflict. `global-json` and `build-props` need `--sdk-version`, a validated value, and `global-json` adds the test runner setting only for SDK 10 and later. Without `--feature` every feature except `signed-commits` is selected, because signing needs a key that the caller provides and fails closed without one. A feature may be repeated; an unknown language or feature is an error that lists the valid IDs.
- **Written files:** the selected baseline files with the managed-file marker, `.config/sharedkernel-repo.json` holding the same baseline section, and, unless a Git repository already exists at the root, `git init` with the chosen branch followed by the Git settings.
- **Atomicity:** the files and the configuration are written in one rollback batch, as `baseline apply` does. A conflict anywhere (an existing unmanaged file, a different Git setting, an existing configuration file) writes nothing and fails, in a preview too. A preview that would fail under `--force` never exits 0.

## Generic by construction

The tool embeds only neutral content. No author, copyright holder, forge, host, or registry appears in it, and none of those is a variable. Content that carries policy or personal data (a license, a security contact, CODEOWNERS, CI workflows) is not part of this slice and would come from consumer sources, as in the baseline ADR.

## Value objects

A language and a feature are sealed records with a private constructor and a `TryParse` that accepts only catalog IDs, so an invalid value cannot reach the planner and `default` cannot exist. The repository name and branch reuse the baseline configuration validation, so one rule covers `new` and `baseline`.

## Deferred

- Drift detection for the written-once files. `baseline verify` ignores them.
- A failure of `git init` or of a Git setting after the file batch leaves the files in place; remove the directory and rerun, or run `git init` and `baseline apply` in it.
- Per-forge files (pull request template path, issue forms, CI workflows) need a forge choice; this follows the deferred `forge` group.
- Further languages.
