# SharedKernel.Testing.CodeFixes

Code-fix project for repository-wide test conventions.

## Purpose

This Roslyn component is reserved for focused fixes that pair with the diagnostics in
`SharedKernel.Testing.Analyzers`.

## Current State

- No testing code fixes are implemented yet.
- `SKTEST001` is diagnostics-only in the current rollout slice.

## Suppression policy

- Prefer supported non-obsolete Roslyn APIs first.
- If a Roslyn package/version gap forces an obsolete API bridge, keep the suppression scoped to the
  smallest possible block and document why that bridge is still required.
- Do not hide broad analyzer or compiler warnings at the project level just to make a code fix build.

## See Also

- [SharedKernel.Testing.Analyzers](../SharedKernel.Testing.Analyzers/README.md)
- `tests/AGENTS.md`
