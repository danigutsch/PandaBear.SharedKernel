# SharedKernel repository configuration tool

`sharedkernel-repo` maintains repository configuration, roadmap structure, and optional GitHub projection.
It is packaged privately as `PandaBear.SharedKernel.RepoConfig.Tool`.

## Commands

- `init`, `verify`, and `diff` initialize or inspect repository planning structure.
- `set github.repository <owner/repo>` sets the configured GitHub repository.
- `get <query>` reads the local roadmap queue and dependency information.
- `sync github --dry-run` previews GitHub projection; `--apply` explicitly performs it.
- `reconcile github` and `intake github` default to previews; `--apply` writes local roadmap state.
- `text-encoding --root <path>` validates indexed UTF-8 text.
- `podman-dns-preflight --check` diagnoses rootless Podman DNS state without modifying it.

Use `--root <path>` for repository commands that target another checkout. Initialization leaves
GitHub integration disabled. Explicit configuration and `--apply` are required for remote changes.
Application CI slices, coverage exclusions, bootstrap, and consumer compatibility remain consumer-owned.

## Roadmap automation

Use `automation plan-github` for an integrity-bound plan. Application workflow configuration and protected-environment approval remain consumer-owned. Applying and publishing require the reviewed plan and artifact digests; publication also requires a sealed result.
