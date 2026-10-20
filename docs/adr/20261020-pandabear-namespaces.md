# PandaBear C# namespaces

Date: 2026-10-20. Status: accepted.

The standalone repository owns reusable libraries independently of ViajantesTurismo. C# namespaces migrate from `SharedKernel.*` to `PandaBear.*`, including generated source and tooling contracts. This deliberately breaks source compatibility; consumers must update imports and qualified names in a separate adoption change.

Projects, directories, assembly identities, solution name, tool command names, and existing `PandaBear.SharedKernel.*` package IDs remain unchanged. Keeping those identities avoids an unrelated package and build-asset migration. Namespaces no longer follow assembly names; namespace-aware tooling must handle that distinction.

Public API baselines must record the removals and additions intentionally. Existing API compatibility and breaking-change gates remain enabled. Conventional Commit history must mark the namespace migration as breaking; the release calculator chooses the highest impact since the latest reachable release tag. Existing immutable candidate packages remain available for rollback.

This supersedes earlier extraction guidance retaining `SharedKernel.*` namespaces. The earlier private-Git restriction is superseded by the [public hosting decision](20261020-public-git-hosting.md). Registry publication, stable tags, and ViajantesTurismo adoption remain separate actions.
