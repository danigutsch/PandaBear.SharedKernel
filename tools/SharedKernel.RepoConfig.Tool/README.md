# SharedKernel repository configuration tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

`sharedkernel-repo` maintains repository configuration, roadmap structure, and optional GitHub projection.
It is packaged privately as `PandaBear.SharedKernel.RepoConfig.Tool`.

## Commands

- `init`, `verify`, and `diff` initialize or inspect repository planning structure.
- `set github.repository <owner/repo>` sets the configured GitHub repository.
- `get <query>` reads the local roadmap queue and dependency information.
- `sync github --dry-run` previews GitHub projection; `--apply` explicitly performs it.
- `reconcile github` and `intake github` default to previews; `--apply` writes local roadmap state.
- `security baseline --root <path>` checks repository-owned Markdown documentation contracts.
- `security scan --root <path> --output <directory>` additionally scans current source with Gitleaks.
  See the [security baseline](../../docs/security-baseline.md) for configuration and exit codes.
- `text-encoding --root <path>` validates indexed UTF-8 text.
- `podman-dns-preflight --check` diagnoses rootless Podman DNS state without modifying it.

Use `--root <path>` for repository commands that target another checkout. Initialization leaves
GitHub integration disabled. Explicit configuration and `--apply` are required for remote changes.
Application CI slices, coverage exclusions, bootstrap, and consumer compatibility remain consumer-owned.

## Roadmap automation

Use `automation plan-github` for an integrity-bound plan. Application workflow configuration and protected-environment approval remain consumer-owned. Applying and publishing require the reviewed plan and artifact digests; publication also requires a sealed result.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.RepoConfig.Tool`. C# namespace: `PandaBear.RepoConfig.Tool`. Assembly/project identity: `SharedKernel.RepoConfig.Tool`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
