# SharedKernel repository configuration tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-repo` maintains repository roadmap configuration, its optional GitHub projection, dependency
snapshots, and repository checks. The tool holds generic engines; the consuming repository owns its policy and
data, such as the license exceptions and documentation link scope in `.config/sharedkernel-repo.json`, its workflows,
and protected-environment approvals.

## Prerequisites

The tool runs on .NET 10. `verify-text-encoding`, `verify-line-endings`, `verify-json`, `verify-gherkin`,
`dependency snapshot`, `dependency verify-license-exceptions`, and the `automation` steps also need Git on an absolute
`PATH` entry.

## Installation

Install a validated candidate as a local tool from the feed that holds it, such as the folder that
`sharedkernel-ci stage-candidate` stages a candidate in. Add the feed to `NuGet.Config` and map
`PandaBear.SharedKernel.*` to it. Map every other package, for example `*`, to the existing source, because
a package without a mapping does not restore once source mapping is enabled. With the SDK pinned in
`global.json`, `dotnet tool install` rejects `--add-source` when package source mapping is enabled.

```text
dotnet tool install PandaBear.SharedKernel.RepoConfig.Tool --version <validated-candidate-version>
dotnet tool run sharedkernel-repo -- <command>
```

.NET tools run in full trust. Install only from trusted manifests and package sources.

Package ID: `PandaBear.SharedKernel.RepoConfig.Tool`. C# namespace: `PandaBear.RepoConfig.Tool`.
Assembly/project identity: `SharedKernel.RepoConfig.Tool`. Source is public; registry publication remains
deferred, and nothing here implies availability on nuget.org.

## Usage

```bash
sharedkernel-repo init
sharedkernel-repo verify --root path/to/repository
sharedkernel-repo set github.repository owner/repository
sharedkernel-repo get next-work --limit 5
sharedkernel-repo get by-label "area: tooling"
sharedkernel-repo github reconcile
sharedkernel-repo github reconcile --force
sharedkernel-repo github link-subissue --repository owner/repository --parent 10 --child 20
sharedkernel-repo github link-subissue --repository owner/repository --parent 10 --child 20 --force
sharedkernel-repo github sync-labels --repository owner/repository
sharedkernel-repo github sync-labels --repository owner/repository --force
sharedkernel-repo automation publish-result --force
sharedkernel-repo verify-podman-dns --verbosity quiet
sharedkernel-repo verify-json
sharedkernel-repo verify-line-endings
sharedkernel-repo verify-links
sharedkernel-repo verify-links roadmap/README.md
sharedkernel-repo verify-gherkin
sharedkernel-repo verify-gherkin tests/Features/booking.feature tests/Features/Tours
sharedkernel-repo dependency submit --snapshot TestResults/dependency-snapshot.json --repository owner/repository
sharedkernel-repo dependency verify-license-exceptions --github-output "$GITHUB_OUTPUT"
```

| Command | Purpose |
| --- | --- |
| `init` | Creates missing roadmap structure files and keeps existing ones. GitHub integration starts disabled. |
| `verify`, `diff` | Verify the roadmap structure, configuration, and items; `diff` reports failures as drift. |
| `set <key> <value>` | Sets `github.repository`. |
| `get <query> [<value>]` | Lists roadmap items for `next-priority`, `next-unblocked`, `next-work`, `next-blockers`, `next-enablers`, `low-hanging-fruit`, `pareto`, `blocking-overview`, `blockers-of <item-id>`, `by-tag <tag>`, or `by-label <label>`, and counts for `tags` or `labels`. `--type` filters by roadmap type; `--limit` caps the list at 10 by default. |
| `github sync` | Projects roadmap items to GitHub issues and Projects. |
| `github reconcile` | Reconciles the GitHub snapshot and resolves transferred issue identities. |
| `github import` | Imports the reconciled issue snapshot into roadmap items. |
| `github link-subissue` | Previews or creates a native GitHub parent/subissue link between existing issues in the same repository. |
| `github sync-labels` | Creates the configured labels that are missing and updates those that differ; see [Repository labels](#repository-labels). |
| `automation <step>` | Runs the integrity-bound GitHub Actions steps `plan-github`, `validate-plan`, `apply-plan`, `seal-result`, `verify-result`, and `publish-result`. `publish-result --force` opens the sealed result as a draft pull request; see [Roadmap publication](#roadmap-publication). |
| `verify-text-encoding` | Fails when a text blob in the Git index is not valid UTF-8 or contains NUL. |
| `verify-podman-dns` | Checks local rootless Podman aardvark-dns state without changing it. |
| `verify-json` | Fails when the working-tree content of a `*.json` file in the Git index is missing, not UTF-8, or not JSON. Comments, trailing commas, and a UTF-8 byte order mark are allowed. Each failure names the path, line, and byte. |
| `verify-line-endings` | Fails when a file in the Git index has CRLF or mixed line endings, staged or in the working tree, while its attributes set `eol=lf`, or LF or mixed line endings in the working tree while they set `eol=crlf`. |
| `verify-links [<path>...]` | Fails on a broken local link or Markdown anchor, a link that leaves the repository, or an issue or pull request reference in documentation files; see [Documentation links](#documentation-links). |
| `verify-gherkin [<path>...]` | Checks tracked Gherkin feature files against the configured rules; see [Gherkin feature files](#gherkin-feature-files). |
| `dependency snapshot` | Generates a dependency snapshot from committed lock files; see [Offline dependency snapshots](#offline-dependency-snapshots). |
| `dependency submit` | Submits a dependency snapshot to the GitHub dependency graph. |
| `dependency verify-license-exceptions` | Verifies the configured license exceptions; see [License exceptions](#license-exceptions). |

`github` commands, `automation publish-result`, and `dependency submit` preview their changes unless
`--force` is given. Lines that describe previewed changes start with `preview:`. The `publish-result`
preview names the repository, base SHA, branch, and changed paths; the `dependency submit` preview names
the snapshot, its size, and the endpoint. Every command except `verify-podman-dns` and `github link-subissue` takes `--root <path>`,
which defaults to the current directory; relative `--snapshot`, `--github-output`, and `verify-links` paths resolve against
it. Run `sharedkernel-repo <command> --help` for the options of each command.

`[diagram]` before a command line prints how the tool parses it without running it, and exits 1 when the
command line does not parse, as in `sharedkernel-repo [diagram] verify-json --root .`.

## Configuration

Commands that apply consumer policy read `.config/sharedkernel-repo.json` under `--root`. The file is
strict JSON of at most 1 MiB with `"version": 1`: property names are camelCase and match exactly, and
comments, trailing commas, and unknown or repeated properties are errors. Paths in it are relative to the
root, use `/`, and have no empty, `.`, or `..` segments; symbolic links are not followed. Errors name the
file and the JSON path of the problem. A command fails when the file lacks its section.

```json
{
  "version": 1,
  "labels": [
    { "name": "type: feature", "color": "a2eeef", "description": "New capability" }
  ],
  "licenseExceptions": {
    "approval": {
      "file": ".config/sharedkernel-candidate.json",
      "values": {
        "packageVersion": "1.0.0-alpha.1",
        "libraryRevision": "0123456789abcdef0123456789abcdef01234567",
        "libraryDirty": false
      }
    },
    "packages": {
      "Example.Package": "2.0.0"
    }
  },
  "links": {
    "include": ["README.md", "docs/*.md"],
    "exclude": ["*/bin/*", "docs/GENERATED*.md"]
  }
}
```

### License exceptions

`dependency verify-license-exceptions` checks the `licenseExceptions` section, which records package
license exceptions that a review approved:

- Each property in `approval.values` must hold the same string or boolean at the top level of the JSON file
  that `approval.file` names. A change to any approved value, such as a new pinned version, needs a new
  review.
- Each package in `packages` must resolve to its approved version in every target of every
  `packages.lock.json` in the Git index, and at least one of them must resolve it. Package IDs ignore case;
  versions match exactly.

Every finding is listed. When none is found, `--github-output <path>` appends
`packages=pkg:nuget/<id>@<version>,...` in the configured order, the format of the
`allow-dependencies-licenses` input of GitHub's dependency review action; a failure appends nothing.

### Documentation links

`verify-links` checks the documentation files that the `links` section selects:

- `include` holds at least one path pattern. Without arguments, the command scans each file whose path matches one.
- `exclude` holds path patterns of files that are never scanned, matched after symbolic links resolve; it may be
  empty.
- Patterns match the whole root-relative path with
  `FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false)`: `*` matches any characters, including
  `/`, and `?` matches one character.

Paths given as arguments replace `include` but not `exclude`. Each names a `.md` or `.txt` file, or a directory whose
`*.md` files are scanned recursively, relative to the root; other paths are skipped. Files resolve through symbolic
links, and a file that resolves outside the root is skipped. The walk does not descend into symbolic links to
directories.

Outside fenced code (lines that start with three backticks or tildes), every line, including inline code and HTML
comments, is checked:

- Inline links `[text](target)` end at the first `)`; reference definitions `[name]: target` and autolinks
  `<target>` that hold `://` or start with `#` are checked too. URLs and `mailto:`, `tel:`, and `urn:` targets are
  not followed.
- A local target is percent-decoded, resolves against the file's directory through symbolic links, and must exist
  inside the root. A target that starts with `/` resolves from the file system root, so it leaves the repository.
- A fragment in a link to a Markdown file must name a heading anchor of that file, ignoring case. Every line that
  starts with `#` is a heading. Its anchor drops HTML tags and `` ` ``, `*`, `_`, `~`, `[`, `]`, `(`, and `)`, is
  lowercased, keeps only ASCII letters, digits, spaces, `.`, `_`, and `-`, and turns each space into `-`; a repeated
  anchor gets the suffix `-1`, `-2`, and so on.
- GitHub issue and pull request URLs, repository-relative `issues/<n>` and `pull/<n>` paths, and bare references
  such as `#123`, `GH-123`, `PR-123`, `issue-123`, and `owner/name#123` are findings. Color values such as
  `fill:#e7f5ff`, URL fragments, and `Unicode Standard Annex #15` or `UAX #15` are not.

Lines, whitespace, lowercasing, and percent-decoding follow Python's string rules, so a UTF-8 byte order mark stays
part of the first line. Each finding names the root-relative path and line of the resolved file; a file that cannot be
read or is not UTF-8 is a finding. On success the command prints how many files it scanned.

### Gherkin feature files

`verify-gherkin` checks the working-tree content of the `*.feature` files in the Git index against the
`gherkin` section, whose properties are all required:

```json
{
  "version": 1,
  "gherkin": {
    "include": ["tests/*"],
    "exclude": ["tests/Generated/*"],
    "allowedTagPatterns": ["@smoke", "@wip", "@ADR:\\d+", "@BC:.+"],
    "forbiddenTags": ["@skip"],
    "featureNameMaxLength": 250,
    "scenarioNameMaxLength": 150
  }
}
```

- Without arguments, the command checks the tracked feature files that an `include` pattern matches. Paths on
  the command line, relative to the root and separated by `/`, replace `include`: each names a tracked feature
  file or a directory that holds one, or the command fails. `exclude` patterns leave files out in both cases.
  Patterns match the whole path case-sensitively, and `*` also matches `/`.
- Each tag must match an `allowedTagPatterns` entry as a whole: the pattern `p` compiles as `\A(?:p)\z` with
  `RegexOptions.NonBacktracking` and a one-second match timeout. A tag in `forbiddenTags` is also reported.
- Name lengths count Unicode code points.

Each finding names the path and line:

- An empty file, or a file that does not end with a newline.
- Trailing whitespace on any line, and a tab on a line that is neither blank nor a comment.
- A tag that is not allowed or is forbidden. Tags are the whitespace-separated words that start with `@` on a line
  that starts with `@`.
- A `Feature:` without a name or with a longer name, a file without a named `Feature:`, and a Feature name that an
  earlier checked file has. Files are checked in ordinal path order.
- A `Scenario:` or `Scenario Outline:` without a name, with a longer name, or with the name of an earlier scenario
  in the file.
- A `Scenario Outline:` without an `Examples:` before the next scenario or the end of the file, reported at that
  line.
- A file without a scenario.

Lines end at LF, CRLF, or CR. A line whose first non-blank character is `#` is a comment, and the text from the
first `#` in any other line is ignored, also inside a name. A keyword counts only at the start of a line and directly
before a colon, or alone, which leaves its name empty. `Example:`, `Scenario Template:`, and `Scenarios:` are not
keywords, so a file whose scenarios all use `Example:` has no scenario. A UTF-8 byte order mark stays part of the
first line, which is then neither a keyword, a tag, nor a comment.

### Repository labels

`github sync-labels --repository owner/name` reads the `labels` section, a nonempty array of labels:

- `name` has 1 to 50 characters, without control characters or surrounding white space. Names must differ
  ignoring case, because GitHub matches label names ignoring case.
- `color` is six hexadecimal digits without `#`.
- `description` has at most 100 characters without control characters, and may be empty.

The command reads every page of the repository's labels from `https://api.github.com/repos/<owner>/<name>/labels`
and compares them with the configured labels. A missing label is created; a label whose name casing, color, or
description differs is updated. Colors compare ignoring case, and a label without a description matches an
empty `description`. Labels that are not configured are never deleted. Without `--force`, the command prints a
`preview:` line for each label it would create or update and a summary, and writes nothing. With `--force`, it
writes each change and prints the same summary.

Both modes read `GH_TOKEN`, then `GITHUB_TOKEN`; reads need Issues read and `--force` needs Issues write
permission. Requests go only to api.github.com, do not follow redirects, read responses of at most 1 MiB, and
time out after 30 seconds each. Errors report status codes without echoing response bodies. A failed write
names its label; changes written before it remain, and rerunning the command applies the rest.

## Roadmap publication

`automation publish-result` publishes the sealed roadmap maintenance result. The draft pull request's body is
`automation.pullRequestBody` from `roadmap/config.json`, a nonblank string of at most 65536 characters, and the
command fails without it. `init` writes the property and its schema; set the body that the repository's pull
request policy requires, such as an issue reference:

```json
"automation": {
  "pullRequestBody": "Refs #123"
}
```

With `--force`, the command requires `GITHUB_OUTPUT` and `GITHUB_STEP_SUMMARY` before it writes to GitHub. It
succeeds only when GitHub returns the draft pull request's URL, which it appends to `GITHUB_OUTPUT` as
`pull_request_url` and to the step summary.

## Transferred issues and external references

A roadmap item's `id` stays stable when its GitHub issue moves. Existing numeric mappings still use
`roadmap/config.json`'s configured repository. Optional item-level fields identify a different current
location and retain historical aliases:

```json
"integrations": {
  "github": {
    "issue": 18,
    "repository": "owner/shared-library",
    "nodeId": "opaque-current-node-id",
    "previousLocations": [{ "repository": "owner/application", "issue": 457 }],
    "previousNodeIds": ["opaque-previous-node-id"]
  }
}
```

These are schema examples; populate identities from GitHub. Node IDs are opaque references and may
change format; do not decode them. Previous locations and node IDs preserve identity across transfers
and ID migrations. Existing aliases do not create new roadmap items. Creation intent
(`"issue": "create"`) cannot include existing node IDs or historical aliases.

`github reconcile` previews resolved locations, aliases, and snapshot changes without writing.
`github reconcile --force` applies the reconciliation manifest and accepted identity updates atomically.
Transfers preserve roadmap IDs, filenames, local status, priority, and relationships. External mappings
are read-only: `github sync` skips their issue and Project mutations. Parent and blocker links continue
to refer to roadmap IDs across repository boundaries.

Cross-repository blockers use the manifest's optional `externalBlockerEdges` array. Each `blocker` and
`blocked` endpoint records `repository`, `issue`, and `state`; exactly one endpoint belongs to the
configured repository. Local numeric `blockerEdges` remain compatible. External edges come from
explicit GitHub blocker metadata, are bound to the accepted snapshot digest and integrity count, and
map to existing qualified roadmap identities without inventing external items. Intake retains both
`blocks` and `blockedBy` links for mapped endpoints; unresolved endpoints require review before
changing affected relationships.

Missing or inaccessible references produce diagnostics; they do not establish closure or deletion.
Unresolved metadata preserves accepted local relationships rather than erasing them. Review affected
references before applying changes, and use explicit closure approvals for actual closed-item transitions.

## Environment

| Variable | Read by |
| --- | --- |
| `GH_TOKEN`, then `GITHUB_TOKEN` | `github` commands and `dependency submit --force`. |
| `GITHUB_TOKEN` | `automation` steps that call GitHub. |
| `GITHUB_REPOSITORY` | `automation` steps. |
| `GITHUB_RUN_ID` | `automation plan-github`. |
| `ROADMAP_PLAN_DIGEST`, `ROADMAP_ARTIFACT_DIGEST`, `ROADMAP_RESULT_DIGEST` | `automation` steps after planning, as each step requires. |
| `GITHUB_OUTPUT`, `GITHUB_STEP_SUMMARY` | `automation plan-github`, `seal-result`, and `publish-result --force`. |

## Limits

- `verify-podman-dns` inspects only local rootless Podman on Linux and fails closed for remote services.
- `verify-text-encoding` checks staged regular files, skips files whose `binary` attribute is set, and
  fails on blobs larger than 64 MiB.
- `verify-gherkin` reads the working-tree content of the regular files in the Git index, skips symbolic links,
  and fails on files larger than 64 MiB. Untracked feature files are not checked, and a tracked file missing from
  the working tree is reported.
- `verify-json` reads the working-tree content of the regular files in the Git index, skips symbolic links
  and submodules, and fails on files larger than 64 MiB. In a sparse checkout, files outside it are
  reported as missing.
- `verify-line-endings` reads the line endings that `git ls-files --eol` reports. Git stores `eol=crlf` text
  with LF in the index, so only the working tree is checked for it. Files missing from the working tree are
  checked only in the index.
- `verify-links` reads the working tree, not the Git index, and fails on files larger than 64 MiB.
- `dependency verify-license-exceptions` reads the working-tree content of the regular `packages.lock.json`
  files in the Git index, and fails on an approval file larger than 1 MiB or a lock file larger than 64 MiB.
- `dependency submit` accepts a snapshot of at most 32 MiB, the size that `dependency snapshot` writes.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success, including previews and a healthy or inapplicable Podman state. |
| 1 | Verification failure or drift, invalid text encoding or JSON, a line-ending, link, or Gherkin finding, a license exception finding, stale or unverifiable Podman DNS state, invalid configuration, a file, Git, or GitHub error, a rejected dependency submission, a command-line error, or cancellation. |
| 130, 143 | Ctrl+C or SIGTERM during start-up, or the command did not stop within two seconds. |

## Security

- Tokens come only from the environment. GitHub transport failures are reported without request details.
- Child processes (Git, Podman, and `stat`) start without `GH_TOKEN` and `GITHUB_TOKEN`.
- Git starts from the first absolute `PATH` entry that holds it, never from the current directory or a
  relative or empty `PATH` entry.
- Without `--force`, `github` commands and `automation publish-result` only read and change nothing in
  GitHub or the checkout; `--force` never skips validation.
- `dependency submit` sends no request and reads no token without `--force`. With it, the command sends
  the snapshot file unchanged and reports only the HTTP status of a rejected request.
- `automation` steps after planning are bound to the reviewed plan digest. `publish-result` requires a
  sealed result with or without `--force`, and its preview reads GitHub only to validate. With `--force`
  it creates Git objects, one new branch, and a draft pull request. When the branch already exists it
  fails and leaves only unreferenced Git objects.
- The `publish-result` preview prints no file contents.
- Output escapes control characters from roadmap and GitHub data.

## Security commands

`security baseline --root <path>` verifies the configured repository document contracts.
`security scan --root <path> --output <directory>` also scans current source with Gitleaks.
See the [security baseline](../../docs/security-baseline.md) for configuration and exit codes.

## Offline dependency snapshots

Generate a GitHub dependency-submission snapshot from the exact clean reviewed commit, without restoring,
loading, or downloading its packages:

```sh
sharedkernel-repo dependency snapshot --root /path/to/reviewed-checkout --sha <commit-sha> --ref refs/heads/main --job <run-id> --correlator solution-nuget --output TestResults/dependency-snapshot.json
```

The command reads the committed Git tree and blobs at the reviewed SHA, rather than trusting working-tree
files or Git's status cache. It inventories every project in the root's unique `.slnx` and requires its
committed `packages.lock.json` in the supported version 1 or 2 layout. Git lazy fetching and replacement
objects are disabled for these reads. Input blobs are limited to 8 MiB each and 64 MiB in aggregate.
Snapshot JSON is limited to 32 MiB; the budget is checked while writing package nodes and edges,
before an amplified graph can be published.

Snapshots contain only genuine NuGet package URLs. Every framework, exact resolved version, direct/transitive
classification, and package edge is retained. Local Project nodes are connectors: any package dependency
through them is expanded to its real package targets, without inventing external project packages.
Centrally pinned transitive packages remain indirect. Project cycles are rejected, traversal is iterative,
and dependency expansion is limited to one million edge visits. Package scope is conservatively `runtime`
because lockfiles do not encode development-only asset usage.

Missing committed locks, unresolved edges, ambiguous names, unsupported node kinds or lock versions,
dirty/mislabeled commits, and unsafe output paths fail before publication. Existing output is not overwritten.
The command itself performs no network calls. One source-built tool can inspect both historical base and
current head checkouts, so retired releases are unnecessary for dependency review. Actual application builds
still require the current candidate's artifacts.

`dependency submit` sends the snapshot to GitHub's dependency submission API:

```sh
sharedkernel-repo dependency submit --snapshot TestResults/dependency-snapshot.json --repository owner/repository --force
```

The snapshot must be a JSON object, and `--repository` must name `owner/name`. With `--force`, the command
posts the file unchanged to `https://api.github.com/repos/<owner>/<name>/dependency-graph/snapshots` with the
token from `GH_TOKEN`, then `GITHUB_TOKEN`; a workflow token needs `contents: write`. A response other than
2xx, or a result of `INVALID`, fails the command.

## Native GitHub subissue links

`github link-subissue --repository owner/repository --parent 10 --child 20` validates the repository
name and positive issue numbers and previews the intended relationship without network access or
credentials. It requires no roadmap or consumer configuration.

Add `--force` to resolve both existing issues, check the child's current parent, create the native
link using the child's numeric REST identity, and verify its parent afterward. An already matching
relationship succeeds without a write. A different parent is rejected; `--force` never reparents.
The command supports issues in one repository on github.com; pull requests are rejected.

Forced linking reads `GH_TOKEN`, then `GITHUB_TOKEN`, and requires access to both issues with Issues
write permission for mutation. It does not invoke `gh` or extract credentials from another process.
Supply the token through the existing environment authentication flow; never pass it as an argument.
Requests go only to api.github.com, do not follow redirects, and bound issue responses to 1 MiB and
the complete operation to 30 seconds. Cancellation is propagated. Errors report status codes without
echoing remote response bodies or transport details.

A successful POST followed by failed verification, or cancellation/transport failure during a write,
may have changed the remote relationship; inspect the child before retrying. Writes are not automatically
retried. A 422 response is accepted only
when a readback proves that a concurrent request created the exact requested link. Exit code 0
means previewed, already linked, or linked and verified; validation, API, verification, and timeout
failures exit 1. No package publication or consumer package adoption is performed by this command.

The API contract follows [GitHub's subissue REST documentation](https://docs.github.com/en/rest/issues/sub-issues).
