# SharedKernel.Blazor.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Analyzers for Blazor component state and service-scope conventions.

## Purpose

Components on Blazor Server live as long as the connection. Two mistakes follow from that and only show up at run time:
a busy flag that an exception leaves set, and services injected into a component that go stale in the connection's one service scope.

Reference this package from projects that contain Blazor components. Only types that implement `IComponent` are analyzed.
Code generated from `.razor` files is analyzed too, so a diagnostic in an `@code` block points at the `.razor` file. The IDE cannot
apply a code fix inside generated code, so move the logic to a code-behind file (`.razor.cs`) to get the fix.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKBLZ001` | Warning | A component flag is set to `true`, awaited across, and reset without `try`/`finally`. |
| `SKBLZ002` | Warning | A component injects `ISender` or a `DbContext`, which go stale in the circuit's service scope. |
| `SKBLZ003` | Warning | A component loop renders table rows, list items or components without a key. |
| `SKBLZ004` | Warning | A routable component reads a parameter in `OnInitialized` or `OnInitializedAsync`, which does not run again when the route changes it. |

## Configuration

```ini
dotnet_diagnostic.SKBLZ001.severity = warning
dotnet_diagnostic.SKBLZ002.severity = warning
dotnet_diagnostic.SKBLZ003.severity = warning
dotnet_diagnostic.SKBLZ004.severity = warning
```

## SKBLZ001

Reports `flag = true;` when a later statement in the same block awaits and a statement in that block, not inside a `finally`,
sets the same field or property back to `false`. The diagnostic points at the assignment that sets the flag.

```csharp
_saving = true;      // SKBLZ001
await Save();
_saving = false;
```

Awaits count when they are `await` expressions, `await foreach` (including deconstruction) and `await using` statements or declarations.
Awaits inside nested lambdas, anonymous methods and local functions never count, including a local function declared between the
set and the reset, because nothing is awaited on that path.

An await inside the `try` block of a try statement is protected, and does not count, when that statement has a catch-all clause
(a bare `catch`, `catch (Exception)` or `catch (object)`) with no `when` filter whose block falls through normally, because the
reset is reached on every path. A catch block that can `throw` (statement or `?? throw` expression), `return`, `goto`, or `break`/`continue`
out of the block skips the reset, so it does not protect (jumps that stay inside the catch block, or inside a nested lambda or local
function, are fine). Awaits in the `catch` or `finally` blocks, after the `try`, or in a `try` that only catches a specific
exception type (for example `IOException`) still count.

```csharp
_saving = true;      // not reported: every path reaches the reset
try { await Save(); }
catch (Exception) { Log(); }
_saving = false;
```

Not reported: a reset inside `finally`, no unprotected await between the two statements, local variables, flags assigned non-literal values,
and types that are not components.

Fix: `SharedKernel.Blazor.CodeFixes` offers "Reset the flag in a finally block" when no local declared in the wrapped statements
(including `out var`, pattern and deconstruction variables) is used after the reset, since wrapping would move it out of scope. `ServerFormSubmission` in `SharedKernel.Blazor` already handles this for forms that save to a server.

## SKBLZ002

Reports a property marked `[Inject]` (this includes `@inject` in `.razor` files) of type `PandaBear.Mediator.ISender` or of a type
derived from `DbContext`, inside a component. The diagnostic points at the property type.

```csharp
[Inject]
public ISender Sender { get; set; } = default!;   // SKBLZ002
```

Not reported: `ScopedSender`, `IDbContextFactory<T>`, properties without `[Inject]`, and types that are not components.

Fix: inject `ScopedSender` (register it with `AddScopedSender()`), or `IDbContextFactory<T>` and create a
context per operation. `SharedKernel.Blazor.CodeFixes` replaces `ISender` with `ScopedSender`, but only when the component sends plain requests (`Send` with an `IRequest<T>`) or does not use the sender; the fix cannot add the required `AddScopedSender()` registration. The diagnostic stays when the component sends an `IStreamRequest<T>`: `ScopedSender` has no stream overload and a stream cannot run in a per-request scope, so create a scope per stream with `IServiceScopeFactory`. There is no automatic fix for
`DbContext`, because the component code that uses it has to change too. `ScopedSender` runs every request in a new service scope;
carry per-circuit state (current user, tenant) into it with the `AddScopedSender` initializer, see the
[Mediator README](../SharedKernel.Mediator/README.md#scoped-sending).

## SKBLZ003

Reports a `foreach` or `for` loop in a component whose body opens a `tr`, an `li` or a component (`OpenComponent`) and never calls
`SetKey`, which is what `@key` compiles to. The diagnostic points at the first such node.

```razor
@foreach (var row in Rows)
{
    <tr>...</tr>       @* SKBLZ003: add @key="row.Id" *@
}
```

Without a key Blazor matches items by position, so after an insert, a removal or a reorder the state of one row (a pending
confirmation, a focused input) can attach to another row. Not reported: loops that call `SetKey`, `option` and other elements, and
types that are not components. There is no code fix, because the key has to be a stable identifier chosen by the author.

## SKBLZ004

Blazor keeps one component instance when the route changes between URLs that map to the same component (for example
`@page "/customers/new"` and `@page "/customers/{Id:guid}/edit"`). `OnInitialized` does not run again, so state loaded from a parameter
goes stale: the new `Id` is ignored and the previous record's values stay. Reports a read of a `[Parameter]` property (this includes
`[SupplyParameterFromQuery]`) inside an override of `OnInitialized` or `OnInitializedAsync`, including lambdas declared in the method.
The diagnostic points at the property access.

```csharp
[Parameter] public Guid? Id { get; set; }

protected override async Task OnInitializedAsync()
{
    _model = await Load(Id);   // SKBLZ004
}
```

Only routable components are checked: the class has a `[Route]` attribute (what `@page` generates) and either a route template with a
`{` parameter segment, more than one `[Route]`, or the property is a `[SupplyParameterFromQuery]` parameter.

Not reported: reads in `OnParametersSet` or `OnParametersSetAsync`, components without a route (a child component whose parameters are set
once), a single route without parameters, properties without `[Parameter]` (including `[CascadingParameter]` only), `nameof(Id)`, assignments to the
parameter, members of other instances, code reached through other methods (the check is syntactic and limited to the method body), and types
that are not components.

There is no code fix, because the right change needs a design decision: load in `OnParametersSetAsync` and reload only when the value
changed (see `ParameterTracker<T>` in `SharedKernel.Blazor`), and decide what happens to unsaved edits.

## Package boundary

This package owns Blazor component diagnostics. Keep repository-wide style rules in `SharedKernel.Style.Analyzers` and domain
encapsulation rules in `SharedKernel.Domain.Analyzers`.

## See Also

- [SharedKernel.Blazor.CodeFixes](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Blazor.CodeFixes/README.md)
- [SharedKernel.Blazor](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Blazor/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor.Analyzers`. C# namespace: `PandaBear.Blazor.Analyzers`. Assembly/project identity: `SharedKernel.Blazor.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
