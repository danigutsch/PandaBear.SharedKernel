# SharedKernel release policy

The repository prepares versioned artifacts locally and in CI. Registry publication is deferred.
No workflow creates a remote, release tag, public NuGet package, or automatic application upgrade.

## Candidates and versions

Successful package-affecting changes on main produce prerelease candidates. Pull requests validate
candidate artifacts before merge. Repository documentation-only changes skip packing; a README
inside a package project remains an artifact input. Unknown history and uncertain inputs select
full validation. Nightly validation covers the complete library and package consumers.

The existing C# `calculate-release` command uses the latest reachable `v*` release tag and the highest
Conventional Commit impact since it: breaking markers select major, `feat` selects minor, and
`fix`, `perf`, or `refactor` select patch. Multiple commits cause one release bump. Without a release
tag, the calculator uses its existing `0.1.0` base and retained history; review that first release
before creating any stable tag. All packages use one coordinated version initially.

CI appends the workflow run number and attempt as an ordered prerelease identifier. Build metadata
alone does not create another NuGet identity. Repository release versions describe meaningful
releases; build identifiers distinguish candidates without bumping the release core on every build.

## Validation and stable preparation

Build once per version and retain the exact packages, symbol archives, candidate manifest, and
SHA-256 hashes. Ordinary packages use isolated restore checks; .NET tools use tool installation.
The Windows job consumes the same artifacts produced on Linux. Keep platform and NativeAOT evidence
separate, and retain unavailable evidence as pending. Never overwrite an existing package version.

Manual `prepare_stable` dispatch prepares and validates a stable identity without publishing it.
A stable identity requires its own artifacts and validation; it cannot be made by relabeling a
prerelease archive. API compatibility, security, promised platform checks, and application evidence
must be reviewed before a later explicit publication. Stable promotion remains pending meanwhile.

ViajantesTurismo adopts a candidate through a separate dependency change with reviewed central
versions, local-tool pins, lock files, and application compatibility evidence. Library merges do
not silently upgrade consumers. Preserve previous artifacts and pins for rollback.

## Tools and agent guidance

Use the C# tools documented in the root README and the versioning tool README. Agent skills
`pandabear-release` and `pandabear-consumer` describe release preparation and application adoption;
installations belong in the normal Codex skills directory. They do not authorize publication.

References: [library versioning](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning),
[NuGet versions](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning), and
[package authoring](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices).
