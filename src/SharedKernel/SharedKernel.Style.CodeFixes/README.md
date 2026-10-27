# SharedKernel.Style.CodeFixes

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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
- `SKSTYLE009` can convert a private command method that only returns `Result.Ok()` to `void`, `Task`,
  or `ValueTask` when all direct callers and result-composition callbacks can preserve the successful
  result.
`SKSTYLE011` deliberately offers no code fix: a safe bound, unit, rejection policy, and ownership
cannot be inferred from a whole-read invocation. Async conversion does not bound allocations;
truncating input or choosing an arbitrary limit changes behavior. Regression tests protect the
absence of individual and bulk actions. See the analyzer README's IO policy for manual remediation.

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

`SKSTYLE010` is intentionally analyzer-only because its remediation must preserve the caller-specific
not-found detail.

## See Also

- [SharedKernel.Style.Analyzers](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md)
- `docs/CODING_GUIDELINES.md`

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Style.CodeFixes`. C# namespace: `PandaBear.Style.CodeFixes`. Assembly/project identity: `SharedKernel.Style.CodeFixes`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

`SKSTYLE014` offers a TimeProvider replacement only when one accessible non-nullable provider is in scope and
the edited project compiles. UTC DateTime and both DateTimeOffset clock reads are supported; local DateTime and
Today require a manual timezone/Kind decision. Ambiguous provider selection has no automatic fix or Fix All.
