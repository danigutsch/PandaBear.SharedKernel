# Release policy

All packages remain preview (prerelease) until stable artifacts are separately authorized. Public source and the explicitly approved [GitHub preview `v1.0.0-alpha.20261021.2`](https://github.com/danigutsch/PandaBear.SharedKernel/releases/tag/v1.0.0-alpha.20261021.2) are available; its immutable packages and candidate manifest retain their original revision and SHA-256 identities. Verify those identities before consumption. This preview distribution does not authorize NuGet registry publication, stable packages, stable tags, or stable releases.

## Candidates and versions

Successful package-affecting changes on main produce `0.0.0-alpha.<run>.<attempt>` candidates. Pull requests validate candidate artifacts before merge. Repository documentation-only changes skip packing; a README inside a package project remains an artifact input. Unknown history and uncertain inputs select full validation. Nightly validation covers the complete library and package consumers.

Use the existing C# `calculate-release` command. For prerelease builds it reports the highest Conventional Commit impact since the latest reachable `v*` release tag, but fixes the package core at `0.0.0` while the library is experimental. Breaking changes remain documented and reported; they do not advance the preview core. A run identifier is required so preview calculation never emits a stable identity. Previously produced preview versions retain their identities and are not overwritten. Moving from an earlier `1.*` or `2.*` preview requires an explicit consumer pin because `0.0.0-alpha.*` sorts lower. Stable calculation and the reusable SemVer calculator retain their Conventional Commit bump behavior; a stable release requires separate authorization.

CI appends workflow run number and attempt as an ordered prerelease identifier. Build metadata alone does not distinguish NuGet identities. One coordinated package version describes the release; unique candidate identifiers distinguish artifacts without incrementing the release core for every build.

## Immutable validation

Build once per version and retain exact packages, symbol archives, candidate manifest, revision, and SHA-256 hashes. Never overwrite an existing package version. Validate ordinary packages through isolated restore/locked restore/build/run checks and tools through installation. The Windows job consumes the same artifacts produced on Linux; keep platform and NativeAOT evidence separate and mark unavailable evidence pending.

CI always calculates prerelease candidates, including manual dispatch; there is no stable-preparation input. Do not create stable packages, tags, or releases now. Generic CLI stable-version capabilities remain available for future separately authorized work. A future stable identity needs its own artifacts and validation; never relabel a prerelease archive.

## API and source compatibility

C# namespaces use `PandaBear.*`; projects, assemblies, command names, and `PandaBear.SharedKernel.*` package IDs retain their documented identities. The [namespace decision](adr/20261020-pandabear-namespaces.md) records the breaking source change. Preserve API compatibility and breaking-change gates and mark deliberate breaks in Conventional Commit history. Keep previous candidates and evidence for rollback.

Release tooling is documented in the root README and [versioning tool guide](../tools/SharedKernel.Versioning.Tool/README.md). Release preparation itself does not authorize registry publication.

References: [library versioning](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning), [NuGet versions](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning), and [package authoring](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices).

## Idempotency contracts package migration

The contracts package is now `PandaBear.SharedKernel.Idempotency.Abstractions`; the existing
`PandaBear.Idempotency` namespace remains unchanged; the project and assembly become
`SharedKernel.Idempotency.Abstractions`.
This is a breaking preview package-identity change. Consumers must replace direct references
and refresh their candidate pins and lock files together; do not install both identities.
The separate `PandaBear.SharedKernel.Idempotency.EntityFrameworkCore` provider now depends on
the renamed contracts package. HTTP retry state belongs to HttpClients; application request
fingerprinting and transaction/replay policy remain in the consuming application.
