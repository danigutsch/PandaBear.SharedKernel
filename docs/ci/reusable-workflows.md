# Reusable workflows and actions

SharedKernel publishes generic CI pieces that its own workflows and consumer repositories call. A consumer
references each piece by full commit SHA of this repository, never by branch or tag, and bumps the SHA like a
dependency: review the diff of the piece, then update every reference together. A reusable piece never receives
secrets implicitly, and a called workflow cannot widen the permissions its caller grants.

Application policy stays in the caller: triggers, concurrency groups, label handling, candidate staging, test
slicing, quality gates, and license allow-list values.

## PR Metadata

`.github/workflows/pr-metadata-reusable.yml` runs the CI tool's `verify-commit-message --event` on a pull request
event, so the squash header and breaking-change footers follow one definition.

| Input | Default | Meaning |
| --- | --- | --- |
| `runner` | `ubuntu-latest` | Runner label of the job. |
| `source-repository` | empty | Repository that holds the CI tool source. Empty builds the tool from the calling repository, as SharedKernel does. A consumer sets `danigutsch/PandaBear.SharedKernel`. |
| `source-ref` | empty | Full 40-character lowercase commit SHA of the tool source. Required when `source-repository` is set; the job fails otherwise. |

The job has `contents: read` and receives no secrets. The tool is built from a sparse checkout of
`tools/SharedKernel.CI.Tool` and `tools/Shared` in locked restore mode with the SDK of that checkout's `global.json`.

The caller owns the trigger, the concurrency group, and the `ci:rerun` label rule, because a called workflow cannot
share the caller's concurrency group without deadlocking. Copy this contract:

```yaml
name: PR Metadata
on:
  pull_request:
    types: [opened, edited, synchronize, reopened, ready_for_review, labeled]
permissions:
  contents: read
concurrency:
  group: pr-metadata-${{ github.event.pull_request.number }}-${{ github.event.action == 'labeled' && github.event.label.name != 'ci:rerun' && github.run_id || 'metadata' }}
  cancel-in-progress: true
jobs:
  metadata:
    name: ${{ github.event.action == 'labeled' && github.event.label.name != 'ci:rerun' && 'Ignored label event' || 'PR Metadata' }}
    if: ${{ github.event.action != 'labeled' || github.event.label.name == 'ci:rerun' }}
    permissions:
      contents: read
    uses: danigutsch/PandaBear.SharedKernel/.github/workflows/pr-metadata-reusable.yml@<full-sha>
    with:
      runner: ${{ vars.CI_UBUNTU_RUNNER }}
      source-repository: danigutsch/PandaBear.SharedKernel
      source-ref: <full-sha>
```

GitHub names the check `<caller job name> / <called job name>`, so the check is `PR Metadata / Validate squash
header`. A branch ruleset that requires the old `PR Metadata` check name must be updated when a caller adopts the
workflow.

SharedKernel's own `.github/workflows/pr-metadata.yml` is this caller with a local `uses:` path.

## .NET toolchain setup

`.github/actions/setup-dotnet-tool` is the one composite action that sets up the SDK and builds a repository-owned
CLI. It runs `actions/setup-dotnet` with `global-json-file` and the NuGet cache keyed by the lock files, then
`dotnet restore <project> --locked-mode` and `dotnet build <project> -c Release --no-restore`. A stale lock file fails
the restore; nothing relaxes it. Steps use `bash`, so the action runs on Linux, macOS, and Windows runners.

| Input | Default | Meaning |
| --- | --- | --- |
| `project` | required | Tool project to restore and build, relative to `working-directory`. |
| `global-json-file` | `global.json` | Path of the `global.json` that pins the SDK, relative to the workspace. |
| `working-directory` | `.` | Directory, relative to the workspace, from which the project is restored and built. |
| `cache-dependency-path` | `**/packages.lock.json` | Glob of the lock files that key the NuGet cache, relative to the workspace. |

With the defaults the action behaves as before, so existing callers keep their cache keys. The PR Metadata workflow
calls it from the checked-out tool source (`./tool-source/.github/actions/setup-dotnet-tool`, with the tool source
in the sparse checkout) and passes `working-directory: tool-source`.

A consumer wrapper calls the action by pinned SHA after it checked out the pinned SharedKernel source, or by local
path from that checkout, and passes its own `project`, `global-json-file`, and `working-directory`. Candidate staging,
local tool restore, `NUGET_PACKAGES`, and developer certificate trust stay in the consumer wrapper.

## Pinned third-party actions

`.github/action-pins.json` is the one manifest of approved third-party Actions. Each entry names the
`owner/repository`, the full 40-character commit SHA, and the release that SHA belongs to. `firstParty` lists
repositories, such as this one, that need a full SHA but no manifest entry or version comment.

```json
{ "repository": "actions/checkout", "sha": "de0fac2e4500dabe0009e67214ff5f5447ce83dd", "version": "v6.0.2" }
```

Every external `uses:` reference in `.github/workflows` and `.github/actions` pins that SHA and repeats the version as
its trailing comment (`uses: actions/checkout@<sha> # v6.0.2`). Tags and branches fail, whatever they name.
`sharedkernel-repo verify-action-pins` checks this and runs in the repository quality job of the validation workflow:

```sh
dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release -- verify-action-pins
dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release -- verify-action-pins --manifest <path>
```

A consumer builds the tool from its pinned SharedKernel revision and passes `--manifest` with the manifest of that
same revision (sparse-check out `.github/action-pins.json` with the tool source), so both repositories fail on the same
drift. The check lists the file, line, and reason for each violation.

To add or change a pin, review the action as the [pin-update review](pin-update-review.md) describes, edit the manifest,
and update every reference in the same change. Do not edit a workflow alone: the check fails on any disagreement. When
SharedKernel bumps a pin, each consumer takes the bump with its next SharedKernel revision pin, then updates its own
references to the new SHA and version comment.

## Actionlint and dependency review

All of these take `runner` (default `ubuntu-latest`), declare `permissions` per job, and receive no secrets. SARIF and
`security-events: write` stay with the caller; none of these workflows requests them.

### `actionlint-reusable.yml`

Job `actionlint` (`contents: read`) checks out, optionally installs shellcheck, installs actionlint from its release,
and runs `actionlint -color`.

| Input | Default | Meaning |
| --- | --- | --- |
| `runner` | `ubuntu-latest` | Runner label; the job installs Linux x64 binaries. |
| `actionlint-version` | `1.7.9` | Release without the leading `v`. |
| `actionlint-sha256` | empty | SHA-256 of the linux_amd64 archive. Empty verifies against the release checksum file. |
| `install-shellcheck` | `true` | Install shellcheck with apt. |

The caller keeps the triggers and path filters. SharedKernel's `actionlint.yml` passes the archive SHA-256.

### `dependency-snapshot-reusable.yml`

Job `snapshot` (`contents: write`, which the calling job must grant) builds the RepoConfig tool, generates a snapshot
from the committed lock files of `target` and submits it. Inputs: `runner`, `target` (`base` or `head`, required),
`correlator` (required), `source-repository`, `source-ref` (a full SHA, required when `source-repository` is set).
It uploads the `dependency-snapshot-<target>` artifact.

### `dependency-review-reusable.yml`

Job `review` (`contents: read`) fails unless `plan-result` is `success` and `snapshot-result` is `success` or
`skipped`, then runs `actions/dependency-review-action` when `review-required` is `true`.

| Input | Default | Meaning |
| --- | --- | --- |
| `runner` | `ubuntu-latest` | Runner label. |
| `review-required` | `'true'` | Caller's decision, for example from a plan job. |
| `plan-result` | `success` | `needs.<plan>.result` of the caller. |
| `snapshot-result` | `success` | `needs.<snapshots>.result` of the caller. |
| `allow-licenses` | required | Comma-separated SPDX identifiers. |
| `fail-on-severity` | `moderate` | Lowest failing vulnerability severity. |
| `verify-license-exceptions` | `false` | Verify the `licenseExceptions` approval and pass its packages as allow-dependencies-licenses. |
| `source-repository`, `source-ref` | empty | RepoConfig tool source, as for PR Metadata; needed only with `verify-license-exceptions`. |

The caller keeps the triggers, concurrency, the `ci:rerun` label rule, the job `if` and `needs`, the plan job (change
selection), the license values, and any evidence handling. Callers read `needs.<job>.result` and pass it in because a
called workflow cannot see the caller's jobs. As with PR Metadata, check names become `<caller job> / <called job>`.
