# SharedKernel repository configuration tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-repo` maintains repository roadmap configuration, its optional GitHub projection, and
repository checks. Application CI slices, coverage exclusions, bootstrap, consumer compatibility,
workflow configuration, and protected-environment approval remain consumer-owned.

## Prerequisites and installation

The tool runs on .NET 10; `verify-text-encoding` also needs Git. Source is public and registry
publication remains deferred, so install a validated candidate from an explicitly configured local or
private feed as a local tool, then run `dotnet tool run sharedkernel-repo -- <command>`. Nothing here
implies availability on nuget.org.

Package ID: `PandaBear.SharedKernel.RepoConfig.Tool`. C# namespace: `PandaBear.RepoConfig.Tool`.
Assembly/project identity: `SharedKernel.RepoConfig.Tool`.

## Usage

```bash
sharedkernel-repo init
sharedkernel-repo verify --root path/to/repository
sharedkernel-repo set github.repository owner/repository
sharedkernel-repo get next-work --limit 5
sharedkernel-repo get by-label "area: tooling"
sharedkernel-repo github reconcile
sharedkernel-repo github reconcile --force
sharedkernel-repo verify-podman-dns --verbosity quiet
```

| Command | Purpose |
| --- | --- |
| `init` | Creates missing roadmap structure files and keeps existing ones. GitHub integration starts disabled. |
| `verify`, `diff` | Verify the roadmap structure, configuration, and items; `diff` reports failures as drift. |
| `set <key> <value>` | Sets `github.repository`. |
| `get <query> [<value>]` | Lists roadmap items for `next-priority`, `next-unblocked`, `next-work`, `next-blockers`, `next-enablers`, `low-hanging-fruit`, `pareto`, `blocking-overview`, `blockers-of <item-id>`, `by-tag <tag>`, or `by-label <label>`, and counts for `tags` or `labels`. `--type` filters by roadmap type; `--limit` caps the list at 10 by default. |
| `github sync` | Projects roadmap items to GitHub issues and Projects. |
| `github reconcile` | Updates the reconciliation manifest from the open GitHub issues. |
| `github import` | Imports the reconciled issue snapshot into roadmap items. |
| `automation <step>` | Runs the integrity-bound GitHub Actions steps `plan-github`, `validate-plan`, `apply-plan`, `seal-result`, `verify-result`, and `publish-result`. |
| `verify-text-encoding` | Fails when a text blob in the Git index is not valid UTF-8 or contains NUL. |
| `verify-podman-dns` | Checks local rootless Podman aardvark-dns state without changing it. |

`github` commands preview their changes unless `--force` is given. Every command except
`verify-podman-dns` takes `--root <path>`, which defaults to the current directory. Run
`sharedkernel-repo <command> --help` for the options of each command.

## Environment

| Variable | Read by |
| --- | --- |
| `GH_TOKEN`, then `GITHUB_TOKEN` | `github` commands. |
| `GITHUB_TOKEN` | `automation` steps that call GitHub. |
| `GITHUB_REPOSITORY` | `automation` steps. |
| `GITHUB_RUN_ID` | `automation plan-github`. |
| `ROADMAP_PLAN_DIGEST`, `ROADMAP_ARTIFACT_DIGEST`, `ROADMAP_RESULT_DIGEST` | `automation` steps after planning, as each step requires. |
| `GITHUB_OUTPUT`, `GITHUB_STEP_SUMMARY` | `automation plan-github`, `seal-result`, and `publish-result`. |

## Limits

- `verify-podman-dns` inspects only local rootless Podman on Linux and fails closed for remote services.
- `verify-text-encoding` checks staged regular files, skips files whose `binary` attribute is set, and
  fails on blobs larger than 64 MiB.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success, including previews and a healthy or inapplicable Podman state. |
| 1 | Verification failure or drift, invalid text encoding, stale or unverifiable Podman DNS state, a file, Git, or GitHub error, a command-line error, or cancellation. |
| 130, 143 | Ctrl+C or SIGTERM when the command does not stop within two seconds. |

## Security

- Tokens come only from the environment. GitHub transport failures are reported without request details.
- Without `--force`, `github` commands only read; `--force` never skips validation.
- `automation` steps after planning are bound to the reviewed plan digest; publication also requires a
  sealed result.
- Output escapes control characters from roadmap and GitHub data.
