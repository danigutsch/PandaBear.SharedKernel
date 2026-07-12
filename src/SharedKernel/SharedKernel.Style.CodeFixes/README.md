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
- `SKSTYLE003` can remove safe `CancellationToken` default values.
- `SKSTYLE005` can rename generic types that repeat generic arity in the type name.
- `SKSTYLE006` can replace broad cancellation filters with `ShouldHandleAsFailure(ct)` when the
  operation token is available.
- `SKSTYLE008` can append the `DomainEvent` suffix to domain event types when the target name is
  available.
Fix All is limited to `SKSTYLE001`.

## Suppression policy

- Prefer supported non-obsolete Roslyn APIs first.
- If a Roslyn package/version gap forces an obsolete API bridge, keep the suppression scoped to the
  smallest possible block and document why that bridge is still required.
- Do not hide broad analyzer or compiler warnings at the project level just to make a code fix build.

## Package boundary

This package owns fixes for `SharedKernel.Style.Analyzers` diagnostics only. It should not become a
catch-all fix package for testing, mediator, or optional-technology diagnostics. Add a fix here only
when the matching style diagnostic has a local, deterministic, safe remediation.

`SKSTYLE009` and `SKSTYLE010` are intentionally analyzer-only. Their remediation changes method
signatures and callers; `SKSTYLE010` must also preserve the caller-specific not-found detail.

## See Also

- [SharedKernel.Style.Analyzers](../SharedKernel.Style.Analyzers/README.md)
- `docs/CODING_GUIDELINES.md`
