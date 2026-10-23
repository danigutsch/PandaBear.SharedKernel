# SharedKernel.Testing.CodeFixes

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Code-fix project for repository-wide test conventions.

## Purpose

This Roslyn component is reserved for focused fixes that pair with the diagnostics in
`SharedKernel.Testing.Analyzers`.

## Current State

- `SKTEST001` is diagnostics-only in the current rollout slice.
- `SKTEST002` offers a conservative rename when an xUnit test method can be converted safely to the underscore naming convention.
- `SKTEST003` adds configured required trait metadata to a test method.
- `SKTEST004` extracts a static helper method to a dedicated helper file when it does not use test-instance state.
- `SKTEST005` adds a placeholder serial collection justification attribute.
- `SKTEST009` replaces trait string literals with the constant identified by the analyzer.
- `SKTEST011` inlines an adjacent one-use assertion lambda and removes its declaration only when semantic rebinding preserves the exact overload. It preserves comments and named arguments; ambiguous or unsafe rewrites and Fix All are unavailable.

- `SKTEST012` rewrites compatible LINQ Single calls to `ShouldHaveSingleItem`, preserving the returned value. Delegate predicates capture their original target through `Invoke`; unsupported expression trees and ambiguous bindings receive no fix.
- `SKTEST014` introduces a collision-free named local and an explicit `ShouldNotBeNull` guard before the assertion. It supports standalone block statements and checks that the rewritten invocation compiles and binds to the repository assertion API.
- `SKTEST013` and `SKTEST015` provide diagnostic guidance; selecting a semantic target or browser assertion migration remains explicit.
- `SKTEST017` adds the forwarding `userMessage` overload after the assertion. It is offered only when the overload binds to the original and to a static `WithUserMessage` helper of the matching shape, no extension `GetAwaiter` makes the result awaitable, the overload adds no compiler warnings or errors, and it covers the assertion; Fix All is unavailable.

The naming fix preserves canonical keywords and diagnostic identifiers. See
[test assertion conventions](../../../docs/TEST_GUIDELINES.md) for examples.

## Suppression policy

- Prefer supported non-obsolete Roslyn APIs first.
- If a Roslyn package/version gap forces an obsolete API bridge, keep the suppression scoped to the
  smallest possible block and document why that bridge is still required.
- Do not hide broad analyzer or compiler warnings at the project level just to make a code fix build.

## Package boundary

This package owns fixes for `SharedKernel.Testing.Analyzers` diagnostics only. Keep fixes local,
deterministic, and safe for test and assertion-library source. Do not add production or
optional-technology fixes here.

## See Also

- [SharedKernel.Testing.Analyzers](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md)
- `tests/AGENTS.md`

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.CodeFixes`. C# namespace: `PandaBear.Testing.CodeFixes`. Assembly/project identity: `SharedKernel.Testing.CodeFixes`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
