# Repo baseline tool

Date: 2026-10-28. Status: accepted.

Repositories on GitHub and on self-hosted forges need the same baseline files: `.gitattributes`, `.editorconfig`, `.gitignore`, a README skeleton, `CODEOWNERS`, issue and pull request templates, and CI workflow wrappers. `sharedkernel-repo baseline verify` reports drift from the configured baseline and `baseline apply` previews or writes it. The engine stays generic: the consuming repository supplies every policy value through `.config/sharedkernel-repo.json`.

## Where the content comes from

An embedded generic profile plus per-file consumer sources.

- The tool embeds neutral content for files that carry no policy: `gitattributes`, `editorconfig`, `gitignore`, `readme`, `pull-request-template`, `issue-form-bug`, and `issue-form-feature`. A configuration entry names the target `path` and either one of these profile IDs or a `source`, a repository-relative file in the consumer repository that supplies the content. Because the consumer chooses the path, the same profile serves `.github/` and `.forgejo/` layouts.
- Policy-bearing files (`CODEOWNERS` owners, CI workflow wrappers) have no embedded profile. They always use a `source`, so no owner, runner label, host name, or registry reaches the tool.
- Rejected: fetching content from a shared template repository (needs network and a trust decision per run), and a profile that is only consumer-supplied (every repository would copy the same neutral files).

## Managed-file marker

Every written file starts with one comment line that holds the SHA-256 of the remaining content: `# Managed by sharedkernel-repo baseline (sha256 <hex>); ...`, or the same inside `<!-- -->` for `.md` files. JSON paths are rejected because JSON has no comments. The marker gives three facts without a state file:

- the file is managed (the marker is present);
- it was not edited by hand (the body hash matches the marker);
- it is current (the whole file equals the desired content).

Statuses are `current`, `missing`, `outdated` (managed and unedited, so safe to update), `modified` (marker present, body edited), and `unmanaged` (exists without a marker, different content). `modified` and `unmanaged` files are conflicts: `apply` never replaces them unless that entry sets `overwriteExisting: true`. A conflict anywhere makes `apply` write nothing. Comparison ignores CRLF versus LF; written files use LF.

## Variables

A closed set, replaced as plain text in profile and source content: `@@repositoryName@@` and `@@defaultBranch@@`, supplied by the `baseline` section. An unknown `@@name@@` is a configuration error. The delimiter avoids the `${{ }}` of workflow files. There are no expressions, includes, or commands, and substituted values are not rescanned.

## Forge-neutral file set

No profile file names a forge. Paths, forge-specific directories, and CI wrappers are consumer choices. The embedded issue forms and pull request template use only the YAML issue form and Markdown formats that GitHub and Forgejo both read.

## Embedded profiles

Each profile is neutral content with no personal data, host, owner, or forge URL. A consumer overrides any of them with a `source`.

| Profile | Content and rationale |
| --- | --- |
| `gitattributes` | `* text=auto eol=lf`, CRLF for `.cmd` and `.bat`, common binary extensions. One line-ending policy for every platform. |
| `editorconfig` | UTF-8, LF, final newline, trimmed whitespace, four-space indent, two for JSON and YAML, tabs for Makefiles. The common editor baseline. |
| `gitignore` | Environment files (`.env*`, except `.env.example`), logs, temporary and swap files, OS and IDE folders. Excludes secrets and noise without naming a language. |
| `readme` | Title, purpose, getting started, and contributing sections using the two variables. |
| `pull-request-template` | Summary, linked issue (`Closes #`), and a short checklist. |
| `issue-form-bug`, `issue-form-feature` | YAML issue forms that GitHub and Forgejo both read. |
| `pre-commit-diff-check` | A hook that runs `git diff --cached --check`, which rejects staged whitespace errors. Only reads the index. |

## Conflict handling

A conflict (`modified` or `unmanaged` without `overwriteExisting`, or a different Git setting without it) exits 1 in a preview too. A preview that would fail under `--force` must not exit 0. A conflict anywhere writes nothing.

## Git settings and hooks (slice 2)

- `gitConfig` lists `{key, value}` settings, written with `git config --local` through an absolute `git` path without a shell, never by editing `.git/config` as text. Only an allowlist of keys is accepted: `init.defaultBranch`, `pull.ff`, `pull.rebase`, `push.autoSetupRemote`, `push.default`, `fetch.prune`, `rebase.autoStash`, `commit.gpgsign`, `tag.gpgsign`, `gpg.format`, `user.useConfigOnly`, `core.autocrlf`, `core.eol`. Every other key is rejected, in particular the ones that run code or redirect authentication (`core.fsmonitor`, `core.sshCommand`, `core.pager`, `core.editor`, `credential.*`, `alias.*`, `include*`, `url.*.insteadOf`, diff, merge, and filter drivers). Values have no control characters and do not start with `-`. A different existing value is a conflict unless the entry sets `overwriteExisting`. The root must be the top level of the repository, so an enclosing repository is never modified.
- `hooks` versions hook files in a tracked directory (default `.githooks`, validated like every path) from a profile or `source`, with the same marker and `overwriteExisting` semantics; the marker follows the shebang line. Files are executable on POSIX, and a lost executable bit is drift. `core.hooksPath` is set to that directory, and this is the only way the baseline sets it. Nothing is written to `.git/hooks`.
- A failed file write removes the directories that apply created, only when they are empty. Settings are set after the files; a failure there leaves the files, and rerunning is idempotent.

## Deferred

- Slice 3: a `forge` command group with GitHub and Forgejo REST clients (repository settings, labels, branch protection), reusing the `github sync-labels` preview and `--force` pattern. It is not part of this change.

Registry publication, releases, and consumer adoption remain separate actions.
