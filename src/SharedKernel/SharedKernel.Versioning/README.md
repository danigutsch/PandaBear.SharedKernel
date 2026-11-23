# SharedKernel.Versioning

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable SemVer and Conventional Commit release-impact primitives, and the release engine of the SharedKernel
repository, which `dotnet release` (`tools/SharedKernel.Versioning.Tool`) wraps.

The version policy types (`SemanticVersion`, `ConventionalCommitParser`, `VersionCalculation`,
`ReleaseVersionCalculator`) are pure and independent from CI, release workflows, and application hosts. The release
engine runs `git`, `dotnet`, and packed tools through `IProcessRunner` and finds them through `IExecutableResolver`
from `PandaBear.SharedKernel.Processes`, so a host passes `SystemProcessRunner` and `PathExecutableResolver.Default`
and a test passes fakes. Each operation takes an options record and writes its result lines to a `TextWriter` that the
host passes; the engine reads no environment variable and no console.

```csharp
using PandaBear.Processes;
using PandaBear.Versioning;

var runner = new SystemProcessRunner();
var resolver = PathExecutableResolver.Default;

var history = new ReleaseHistory(runner, resolver);
var release = await history.CalculateVersion(new ReleaseVersionOptions(repoRoot, "prerelease", RunNumber: "42"), ct);

var candidates = new CandidateValidator(runner, resolver, TimeProvider.System);
var manifest = await candidates.Validate(new CandidateOptions(repoRoot, NativeAot: true), Console.Out, Console.Error, ct);
```

- `ReleaseHistory` calculates the next version from the commits since the latest `v*` tag (`CalculateVersion`) and
  finds Conventional Commit breaking-change markers in a revision range (`HasBreakingChangeMarker`).
- `SharedKernelPackager` packs every SharedKernel project (`Pack`), packs with package validation and writes the API
  compatibility report (`CheckApiCompatibility`), packs working-tree changes into a dev feed and evicts them from the
  NuGet cache (`PackDev`), and audits the packages that the tool packages of a feed carry, installs the tools, and
  builds and runs the samples against the feed (`ConsumePackages`).
- `CandidateValidator` creates a candidate from a clean checkout (`Create`), consumes it on the current platform and
  records the result (`Consume`), does both (`Validate`), and merges per-platform evidence files (`MergeEvidence`).
- `ConsumerVerifier` runs a consumer repository's command on a clean clone of its committed revision with a
  candidate's packages staged, and records the consumer compatibility evidence (`Verify`).

The engine is NuGet- and `dotnet`-specific by purpose: it versions, packs, and validates this repository's .NET
packages. There is no second package ecosystem or forge behind it, so it has no provider libraries.

## Candidate manifests

`CandidateManifest` is the `candidate.json` of a package candidate. `Find` locates the single manifest under a
directory; `Load` reads it and verifies that every package in `packageSha256` lies beside it with an unchanged SHA-256
hash; `Evidence` is its JSON content, to which validation and consumption add their records; `Save` replaces the file
through a temporary file, so an interrupted write never leaves a truncated manifest. `HashPackage` computes the
lowercase hexadecimal hash that the manifest records.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning`. C# namespace: `PandaBear.Versioning`. Assembly/project identity: `SharedKernel.Versioning`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
