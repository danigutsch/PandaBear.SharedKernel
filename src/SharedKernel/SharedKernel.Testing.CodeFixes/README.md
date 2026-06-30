# SharedKernel.Testing.CodeFixes

Code-fix project for repository-wide test conventions.

## Purpose

This Roslyn component is reserved for focused fixes that pair with the diagnostics in
`SharedKernel.Testing.Analyzers`.

## Current State

- `SKTEST001` is diagnostics-only in the current rollout slice.
- `SKTEST002` offers a conservative rename when an xUnit test method can be converted safely to the underscore naming convention.
- `SKTEST003` adds configured required trait metadata to a test method.
- `SKTEST005` adds a placeholder serial collection justification attribute.

## Suppression policy

- Prefer supported non-obsolete Roslyn APIs first.
- If a Roslyn package/version gap forces an obsolete API bridge, keep the suppression scoped to the
  smallest possible block and document why that bridge is still required.
- Do not hide broad analyzer or compiler warnings at the project level just to make a code fix build.

## Package boundary

This package owns fixes for `SharedKernel.Testing.Analyzers` diagnostics only. Keep fixes local,
deterministic, and safe for test source. Do not add production or optional-technology fixes here.

## See Also

- [SharedKernel.Testing.Analyzers](../SharedKernel.Testing.Analyzers/README.md)
- `tests/AGENTS.md`
