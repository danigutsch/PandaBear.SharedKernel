# SharedKernel.Testing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable test traits and attributes for SharedKernel test suites.

## Trait taxonomy

- `TestTraitNames` owns cross-project trait names such as `Scope`, `Area`, `Category`, `Host`, and
  `Surface`.
- `TestTraitValues` owns cross-project values reused by multiple bounded-context test projects.
- `SharedKernelTestTraitNames` owns SharedKernel package-test names and the neutral `Unit` scope.
- Project-specific values belong in the owning test project's `TestTraits` type.

Use constants in `Trait` attributes whenever they exist. `SharedKernel.Testing.Analyzers` reports
hardcoded trait literals through `SKTEST009` when a single safe replacement can be identified.

## Serial test justification

Use `SerialTestJustificationAttribute` on xUnit collection definitions that disable parallelization.
`SKTEST005` enforces a non-empty reason.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing`. C# namespace: `PandaBear.Testing`. Assembly/project identity: `SharedKernel.Testing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
