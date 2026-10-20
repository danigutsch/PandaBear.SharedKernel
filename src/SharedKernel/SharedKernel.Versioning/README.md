# SharedKernel.Versioning

Reusable SemVer and Conventional Commit release-impact primitives.

The package keeps version policy independent from CI, release workflows, and application hosts.

It also owns reusable release-artifact, local package-feed, and public API baseline helpers used by
repo-owned tools. Tool projects should keep command parsing, process execution, and standard IO wiring
outside this package.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning`. C# namespace: `PandaBear.Versioning`. Assembly/project identity: `SharedKernel.Versioning`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
