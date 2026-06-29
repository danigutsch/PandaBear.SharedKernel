# SharedKernel.Style.CodeFixes

Code-fix project for repository-wide SharedKernel style conventions.

## Purpose

This Roslyn component provides focused, safe code fixes for the diagnostics in
`SharedKernel.Style.Analyzers`.

## Current State

- `SKSTYLE001` can rename a method and its references to remove the `Async` suffix safely.
- `SKSTYLE002` can rename a `CancellationToken` parameter and its references to `ct`
  when the rename does not conflict with an existing `ct` parameter or a `ct`
  declaration in the containing executable scope.
- `SKSTYLE005` can add or replace image pin calls with uncompilable placeholders so the developer must
  fill in a verified tag or bare SHA-256 digest before the code builds.

Fix All is limited to `SKSTYLE001`.

## Suppression policy

- Prefer supported non-obsolete Roslyn APIs first.
- If a Roslyn package/version gap forces an obsolete API bridge, keep the suppression scoped to the
  smallest possible block and document why that bridge is still required.
- Do not hide broad analyzer or compiler warnings at the project level just to make a code fix build.

## See Also

- [SharedKernel.Style.Analyzers](../SharedKernel.Style.Analyzers/README.md)
- `docs/CODING_GUIDELINES.md`
