# SharedKernel.Testing.Snapshots

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable snapshot helpers for SharedKernel test suites.

- `SnapshotText` normalizes snapshot text before comparison.
- `JsonSnapshotArtifactSet` compares generated JSON artifacts with committed canonical snapshots.

## Usage

Configure `JsonSnapshotArtifactSet` with canonical/generated directories and artifact naming rules, then call `AssertCanonicalArtifactsMatchGeneratedArtifacts` or inspect `GetArtifactDrift`. `SnapshotText` provides reusable snapshot text handling. Review canonical snapshot updates intentionally rather than automatically accepting drift.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Snapshots`. C# namespace: `SharedKernel.Testing.Snapshots`. Assembly/project identity: `SharedKernel.Testing.Snapshots`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Reproduction manifests

Use `ReproductionManifestCodec` to persist approved test-attempt metadata. Supply the
fixture revision, scenario, optional seed and allowed outcomes with their exact resource states.
The codec accepts only the versioned schema and matching metadata, bounds reads and writes
to 64 KiB, and publishes each attempt once under its opaque ID. Keep export destinations and
the choice to export in the fixture. The manifest carries no request, response, or session payloads.

The version 1 format and validation originated in the ViajantesTurismo test fixtures.
Their MIT notice is included in the package.
