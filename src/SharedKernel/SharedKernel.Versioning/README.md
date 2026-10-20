# SharedKernel.Versioning

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable SemVer and Conventional Commit release-impact primitives.

The package keeps version policy independent from CI, release workflows, and application hosts.

It also owns reusable release-artifact, local package-feed, and public API baseline helpers used by
repo-owned tools. Tool projects should keep command parsing, process execution, and standard IO wiring
outside this package.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning`. C# namespace: `PandaBear.Versioning`. Assembly/project identity: `SharedKernel.Versioning`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
