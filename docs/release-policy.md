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

## Reconciled application work

The reusable HTTP outcome, bounded-response, and upload-progress changes were reconciled from
ViajantesTurismo commit `daa099e0`; roadmap automation and bounded GitHub reconciliation were
reconciled from `298e3c2d`. Application code and workflow policy remain consumer-owned. The older
July dependency branch does not override the newer platform versions here; Roslyn retains the
intentional compiler API baseline documented in the root README.

The follow-up reconciliation from ViajantesTurismo commit `17586ece` retains the newer roadmap
deadline, cancellation, and bounded-response regressions and applies namespace-aware MSBuild
selection to the standalone repository selector. SDK bootstrap scripts and application CI
selection remain consumer-owned.

## Standalone namespace migration

C# namespaces now use `PandaBear.*`; projects, assemblies, tool commands, and `PandaBear.SharedKernel.*` package IDs retain their existing identities. The [namespace ADR](adr/20261020-pandabear-namespaces.md) records the breaking source change. Keep API compatibility gates enabled and record breaking Conventional Commit evidence. Git repository publication is a separate, explicitly authorized operation to a verified private host/owner/name; package registry publication and stable tags remain deferred.

The current reconciliation includes reusable changes from merged ViajantesTurismo donor `c5f48c521af5ccb14907960d52a8b34c0e7048ab` and the deliberate `PandaBear.*` namespace migration. Earlier donor revisions above remain extraction history; they do not imply that the standalone library still exposes the superseded HTTP outcome names. Validate a new immutable candidate for these changes before any separate application adoption.
