# SharedKernel.Versioning

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable SemVer and Conventional Commit release-impact primitives.

The package keeps version policy independent from CI, release workflows, and application hosts.

It also owns reusable release-artifact, local package-feed, and public API baseline helpers used by
repo-owned tools. Tool projects should keep command parsing, process execution, and standard IO wiring
outside this package.

## Candidate manifests

`CandidateManifest` is the `candidate.json` of a package candidate. `Find` locates the single manifest under a
directory; `Load` reads it and verifies that every package in `packageSha256` lies beside it with an unchanged SHA-256
hash; `Evidence` is its JSON content, to which validation and consumption add their records; `Save` replaces the file
through a temporary file, so an interrupted write never leaves a truncated manifest. `HashPackage` computes the
lowercase hexadecimal hash that the manifest records.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Versioning`. C# namespace: `PandaBear.Versioning`. Assembly/project identity: `SharedKernel.Versioning`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
