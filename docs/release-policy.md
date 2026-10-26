# Release policy

All packages remain preview (prerelease) until stable artifacts are separately authorized. Source is public. Preview candidates are the hashed `candidate-packages` CI artifacts; verify their revision and SHA-256 identities before consumption. Candidates do not authorize NuGet registry publication, GitHub releases, stable packages, stable tags, or stable releases.

Consuming repositories take a candidate from its GitHub prerelease, `v<packageVersion>`, which holds the candidate's packages and manifest, `candidate.json`, unchanged. Each such publication is separately authorized. A consumer pins that manifest and stages the packages with `sharedkernel-ci stage-candidate --repository`.

## Candidates and versions

Successful package-affecting changes on main produce `<target>-alpha.<run>.<attempt>` candidates. Pull requests validate candidate artifacts before merge. Repository documentation-only changes skip packing; a README inside a package project remains an artifact input. Unknown history and uncertain inputs select full validation. Nightly validation covers the complete library and package consumers.

Use the C# `calculate-release` command. Prerelease builds follow the target-version convention: the version core names the release the previews lead to, and only the prerelease identifier advances between candidates. Without a reachable `v*` tag the target is the initial development version `0.1.0`. When the latest reachable `v*` tag is a prerelease, its version core is the fixed target, whatever the commit types since then. After a stable tag, the highest Conventional Commit impact since that tag selects the next target on each run until a prerelease tag fixes it. Breaking changes remain documented and reported in `release_impact`. Moving to a new target is a deliberate release decision recorded by a new prerelease tag. A run identifier is required so preview calculation never emits a stable identity. Previously produced preview versions retain their identities and are not overwritten. Stable calculation releases the same target core, applying a Conventional Commit bump only after a stable tag; the reusable SemVer calculator always applies the bump. A stable release requires separate authorization.

CI appends workflow run number and attempt as an ordered prerelease identifier. Build metadata alone does not distinguish NuGet identities. One coordinated package version describes the release; unique candidate identifiers distinguish artifacts without incrementing the release core for every build.

## Immutable validation

Build once per version and retain exact packages, symbol archives, candidate manifest, revision, and SHA-256 hashes. Never overwrite an existing package version. Validate ordinary packages through isolated restore/locked restore/build/run checks and tools through installation. The Windows job consumes the same artifacts produced on Linux; keep platform and NativeAOT evidence separate and mark unavailable evidence pending. A manual dispatch with `candidate_version` stages that version from its GitHub prerelease and consumes it unchanged on Linux and Windows, uploading a `consumption.json` per platform. A consumer records those results in its pin with `sharedkernel-version merge-evidence --candidate <pin> --evidence <consumption.json>...` instead of editing the pin by hand; the release manifest stays unchanged.

CI calculates only prerelease candidates, including on manual dispatch; there is no stable-preparation input. Do not create stable packages, tags, or releases now. Generic CLI stable-version capabilities remain available for future separately authorized work. A future stable identity needs its own artifacts and validation; never relabel a prerelease archive.

## Channels, promotion, and support

The only release channel is the preview candidate: a prerelease version that signals an unstable API under [SemVer](https://semver.org/) and that NuGet resolves only when a consumer opts in. Stable packages form a second channel that does not exist yet.

A candidate becomes stable only when all of these hold: the same hashed artifacts are consumed on Linux and Windows, including NativeAOT; the API compatibility and breaking-change gates pass; one consumer compatibility run is recorded with `sharedkernel-version verify-consumer`; and the repository owner separately authorizes the release.

Preview candidates promise no compatibility with one another. Deliberate breaks are marked as breaking in Conventional Commit history, and earlier candidates and their evidence are kept for rollback. The first stable release defines the public API and is versioned `1.0.0`: record a prerelease tag whose version core is `1.0.0` before it, so the calculated target follows.

The repository owner owns support for every released version. [Security support](../SECURITY.md) follows the same channels.

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
