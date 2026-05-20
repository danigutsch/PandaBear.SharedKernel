# SharedKernel.Mediator.CodeFixes

Code-fix project for `SharedKernel.Mediator`.

## Purpose

This Roslyn component is reserved for focused fixes that pair with the analyzer diagnostics in the
mediator toolchain.

## Current State

The project now provides safe fixes for the currently implemented generator diagnostics:

- `SKMED001` can generate a missing handler file when no handler candidate exists.
- `SKMED003` can repair the explicit-interface-only handler shape by adding a public forwarding
  `Handle(...)` method.
- `SKMED004` can add the missing public `CancellationToken ct` parameter to a handler `Handle(...)`
  method.
- `SKMED006` can replace an incorrect forwarded cancellation token with the in-scope `ct`.
- `SKMED007` can add `[EnumeratorCancellation]` to async stream handler and pipeline
  `Handle(...)` iterator parameters.
- `SKMED010` can either make an inaccessible registration type public or add
  `InternalsVisibleTo(...)`.
- `SKMED011` can add `[assembly: MediatorModule]` for cross-assembly discovery.

The project also provides safe fixes for closely related compiler diagnostics:

- `CS7036` can add the missing `ct` argument to mediator `Send(...)` calls.
- `CS1503` can add the required mediator request interface to a request type used in `Send(...)`.

Fix All is intentionally limited to the safe bulk-fix set:

- `CS7036`
- `CS1503`
- `SKMED001`
- `SKMED004`
- `SKMED007`
- `SKMED011`

Fix All stays out of behavioral or ordering changes, even when a future local code action might
exist. Those categories remain intentionally manual by default:

- exception-to-`Result<T>` conversions, which would change error-shaping semantics and belong with
  later result-integration work
- notification ordering repairs such as `SKMED200` and `SKMED201`
- pipeline ordering or applicability repairs such as `SKMED020`, `SKMED021`, `SKMED022`, and
  `SKMED023`
- architecture-rule changes such as `SKMED500`

Unsafe or cross-assembly diagnostics remain intentionally unfixed until a safe local repair exists.

## Planned Scope

- Add or repair missing mediator declarations
- Correct invalid registration shapes where a safe fix is possible
- Keep analyzer and code-fix rollout aligned by diagnostic family

## Dependencies

- `Microsoft.CodeAnalysis.Analyzers`
- `Microsoft.CodeAnalysis.CSharp`
- `Microsoft.CodeAnalysis.CSharp.Workspaces`

## See Also

- [SharedKernel.Mediator.Analyzers](../SharedKernel.Mediator.Analyzers/README.md)
- [SharedKernel.Mediator.SourceGenerator](../SharedKernel.Mediator.SourceGenerator/README.md)
