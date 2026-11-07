# SharedKernel.Blazor.CodeFixes

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Code fixes for the [SharedKernel.Blazor.Analyzers](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Blazor.Analyzers/README.md) diagnostics.

## Fixes

| Diagnostic | Fix | Offered when |
| --- | --- | --- |
| `SKBLZ001` | Reset the flag in a finally block | No local declared between the set and the reset (including `out var`, pattern and deconstruction variables) is used after the reset. |
| `SKBLZ002` | Use ScopedSender | The injected type is `PandaBear.Mediator.ISender`. There is no fix for `DbContext`. Register it with `AddScopedSender()`; carry per-circuit state into each request's new scope with its initializer (see the [Mediator README](../SharedKernel.Mediator/README.md#scoped-sending)). |

Fixes are offered in the IDE for code-behind and plain C# files, not inside code generated from `.razor` files.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor.CodeFixes`. C# namespace: `PandaBear.Blazor.CodeFixes`. Assembly/project identity: `SharedKernel.Blazor.CodeFixes`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
