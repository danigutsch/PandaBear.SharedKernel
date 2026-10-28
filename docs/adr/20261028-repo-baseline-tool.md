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

## Deferred

- Slice 2: git configuration and hook installation (`baseline` stays file-only until then).
- Slice 3: a `forge` command group with GitHub and Forgejo REST clients (repository settings, labels, branch protection), reusing the `github sync-labels` preview and `--force` pattern.

Registry publication, releases, and consumer adoption remain separate actions.
