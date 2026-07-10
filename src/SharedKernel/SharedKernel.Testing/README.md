# SharedKernel.Testing

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
