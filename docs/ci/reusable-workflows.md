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
