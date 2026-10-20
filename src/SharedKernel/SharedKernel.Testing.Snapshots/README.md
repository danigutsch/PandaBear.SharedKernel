# SharedKernel.Testing.Snapshots

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable snapshot helpers for SharedKernel test suites.

- `SnapshotText` normalizes snapshot text before comparison.
- `JsonSnapshotArtifactSet` compares generated JSON artifacts with committed canonical snapshots.

## Usage

Configure `JsonSnapshotArtifactSet` with canonical/generated directories and artifact naming rules, then call `AssertCanonicalArtifactsMatchGeneratedArtifacts` or inspect `GetArtifactDrift`. `SnapshotText` provides reusable snapshot text handling. Review canonical snapshot updates intentionally rather than automatically accepting drift.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Snapshots`. C# namespace: `PandaBear.Testing.Snapshots`. Assembly/project identity: `SharedKernel.Testing.Snapshots`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
