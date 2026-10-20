# Release policy

All packages remain preview (prerelease) until stable artifacts are separately authorized. Source hosting is public at `https://github.com/danigutsch/PandaBear.SharedKernel`; registry publication remains deferred. Git source publication does not authorize publishing packages, stable tags, or releases.

## Candidates and versions

Successful package-affecting changes on main produce prerelease candidates. Pull requests validate candidate artifacts before merge. Repository documentation-only changes skip packing; a README inside a package project remains an artifact input. Unknown history and uncertain inputs select full validation. Nightly validation covers the complete library and package consumers.

Use the existing C# `calculate-release` command. It selects the highest Conventional Commit impact since the latest reachable `v*` release tag: breaking markers select major, `feat` selects minor, and `fix`, `perf`, or `refactor` select patch. Multiple commits cause one coordinated release bump. Without a release tag, the existing calculator uses its `0.1.0` base and retained history; review the first stable release explicitly. Do not change the calculator merely to relabel an alpha suffix: alpha is already prerelease.

CI appends workflow run number and attempt as an ordered prerelease identifier. Build metadata alone does not distinguish NuGet identities. One coordinated package version describes the release; unique candidate identifiers distinguish artifacts without incrementing the release core for every build.

## Immutable validation

Build once per version and retain exact packages, symbol archives, candidate manifest, revision, and SHA-256 hashes. Never overwrite an existing package version. Validate ordinary packages through isolated restore/locked restore/build/run checks and tools through installation. The Windows job consumes the same artifacts produced on Linux; keep platform and NativeAOT evidence separate and mark unavailable evidence pending.

CI always calculates prerelease candidates, including manual dispatch; there is no stable-preparation input. Do not create stable packages, tags, or releases now. Generic CLI stable-version capabilities remain available for future separately authorized work. A future stable identity needs its own artifacts and validation; never relabel a prerelease archive.

## API and source compatibility

C# namespaces use `PandaBear.*`; projects, assemblies, command names, and `PandaBear.SharedKernel.*` package IDs retain their documented identities. The [namespace decision](adr/20261020-pandabear-namespaces.md) records the breaking source change. Preserve API compatibility and breaking-change gates and mark deliberate breaks in Conventional Commit history. Keep previous candidates and evidence for rollback.

Release tooling is documented in the root README and [versioning tool guide](../tools/SharedKernel.Versioning.Tool/README.md). Release preparation itself does not authorize registry publication.

References: [library versioning](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning), [NuGet versions](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning), and [package authoring](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices).
