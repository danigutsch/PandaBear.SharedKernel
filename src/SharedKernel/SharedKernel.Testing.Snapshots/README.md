# SharedKernel.Testing.Snapshots

Reusable snapshot helpers for SharedKernel test suites.

- `SnapshotText` normalizes snapshot text before comparison.
- `JsonSnapshotArtifactSet` compares generated JSON artifacts with committed canonical snapshots.

## Usage

Configure `JsonSnapshotArtifactSet` with canonical/generated directories and artifact naming rules, then call `AssertCanonicalArtifactsMatchGeneratedArtifacts` or inspect `GetArtifactDrift`. `SnapshotText` provides reusable snapshot text handling. Review canonical snapshot updates intentionally rather than automatically accepting drift.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Snapshots`. C# namespace: `PandaBear.Testing.Snapshots`. Assembly/project identity: `SharedKernel.Testing.Snapshots`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
