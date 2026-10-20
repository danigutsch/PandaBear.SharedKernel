# Action and container pin review

Keep external Actions pinned to immutable full commit SHAs and container resources pinned to readable tags plus verified digests. Source declarations are authoritative; copied version inventories become stale. This is a manual review workflow, not a new updater or write-capable service.

## Standalone ownership

| Pin location | Review owner and evidence |
| --- | --- |
| `.github/workflows/ci.yml` external `uses:` | Library CI maintainers verify upstream SHA, release changes, permissions, nested Actions, downloaded tools, runner compatibility, and artifact behavior. |
| `src/SharedKernel/SharedKernel.Aspire.Hosting.ClamAv`, `.SeaweedFs`, and `.Grafana` resource extensions | Hosting package maintainers verify registry tag/digest, supported architectures, configuration compatibility, resource health, persistence, and telemetry/privacy behavior. |
| `tools/SharedKernel.PlaywrightMcp.Tool/PlaywrightMcpCommand.cs` | Tool maintainers review the pinned image, local engine isolation, explicit image preparation, network restrictions, and browser/MCP compatibility. |

This checkout has no tracked Dependabot configuration or Dev Container configuration. Do not imply that bots update these C# image constants or that an application updater covers library-owned declarations. SDK and NuGet changes follow the existing central version/locked-restore workflow and [release policy](../release-policy.md).

## Review evidence

For an Action update, record trusted upstream repository, old/new full SHAs, resolved release tags, release notes, and compare link. Review `action.yml`, bundled code, composite steps, downloaded payloads, and nested Actions. Call out new direct/transitive dependencies and supply-chain risk. Check token permissions, secrets, credential persistence, runner requirements, and fork safety; a dependency update does not justify new write permissions.

For an image update, record trusted registry/repository, old/new tags, verified manifest digests, and whether each digest identifies a multi-platform index or platform manifest. Verify required architectures and upstream release/security notes. Review entrypoint, user, ports, volumes, health checks, configuration, data-format migrations, and available vulnerability/provenance evidence. Label unavailable evidence explicitly. Do not invent digests or replace a digest with a moving tag to make startup succeed.

Run Actionlint for workflow changes and relevant package integration/smoke checks for image changes. Preserve sanitized evidence on the exact proposed revision. Successful pulling alone does not establish compatibility. Consumers still own application runtime configuration and validation when adopting updated hosting packages.

Restore the previous reviewed pin when an update fails before rollout. After persistent data changes, reverting an image can be unsafe: follow reviewed backup/forward-recovery procedures and identify possible data loss before resetting or restoring. Do not delete volumes as routine validation. Any future write-capable updater needs separate credential, permission, input-trust, maintenance, and rollback review; this guidance authorizes no remote mutations.

Reference: [GitHub secure use of Actions](https://docs.github.com/en/actions/reference/security/secure-use).

Adapted from ViajantesTurismo commit `68ce1876d69af7dd9527330af845a84a6c3df434` (2026-10-20). Library maintainers own this guidance; application workflows and feature migration plans remain consumer-owned.
