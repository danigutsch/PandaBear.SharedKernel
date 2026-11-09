# SharedKernel.Mediator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Runtime support surface for `SharedKernel.Mediator`.

## Purpose

This project is the runtime-side companion to the abstractions package. It is the place for
concrete mediator services and lightweight runtime helpers that should remain available even when
source generation owns the main dispatch path.

## Current State

The consumer-side source generator now emits the `AppMediator` shell used by generated DI
registration.
This runtime package stays focused on shared abstractions-facing runtime support that can remain
available while source generation owns mediator composition and request dispatch.

The package now also exposes an optional activity-based observability behavior:

- `SharedKernelMediatorActivitySource` defines the stable `ActivitySource` name
  `SharedKernel.Mediator`.
- `ActivityBehavior<TRequest, TResponse>` adds an internal request activity around mediator pipeline
  execution.

## Concurrency conflict handling

`ConcurrencyConflictBehavior<TRequest, TResponse>` lets a command that returns `Result` or `Result<T>` stay free of try/catch for
optimistic-concurrency failures. When the request throws `PandaBear.BuildingBlocks.ConcurrencyConflictException` (a row changed or
disappeared since it was read), the behavior returns a failed result with `ResultStatus.Conflict` and the fixed message
`The data was changed or removed by someone else. Reload it and try again.`; the HTTP and UI edges then map it through the existing
result status mapping (409 Conflict).

- **Converts:** only `ConcurrencyConflictException`. The exception text and inner exception are never copied into the result.
- **Does not convert:** any other exception, including `OperationCanceledException`, which propagate unchanged. A returned result,
  successful or failed, passes through untouched. It applies to `IResultLike<TResponse>` responses only.
- **Logging:** the behavior does not log; it has no logger dependency. Persistence layers that need diagnostics should log where the
  exception is thrown.
- **Registration:** the package assembly is not a mediator module, so the generator does not discover the class. Derive a sealed class in
  your own module assembly (as with `ResultTransactionBehavior`):

```csharp
[PipelineOrder(PipelineStage.Transaction, Order = -100)]
public sealed class ConflictBehavior<TRequest, TResponse> : ConcurrencyConflictBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : struct, IResultLike<TResponse>;
```

  For a manually composed pipeline, `services.AddConcurrencyConflictHandling<TRequest, TResponse>()` registers the closed behavior; calling
  it twice for the same pair adds nothing.
- **Ordering:** lower stage and order run first (outermost). Place the behavior outside the transaction behavior (a lower order within
  `PipelineStage.Transaction`, or an earlier stage), so the transaction rolls back first and the conflict is converted afterwards. Inside the
  transaction, a conflict raised while the transaction commits or is retried would escape the conversion, and handlers would run against a
  transaction that the exception has already invalidated.

- **Stale data found by the handler:** when a handler compares the loaded version with the caller's and finds it outdated, return
  `ConcurrencyConflictResults.Stale()` (or `Stale<T>()`). It is the same `Conflict` result and message as the behavior's, so a stale form and a
  lost race look identical.


## Unique violation handling

`UniqueViolationBehavior<TRequest, TResponse>` is the race-safety net for "create if it does not exist yet" commands that return
`Result` or `Result<T>`. When two concurrent requests pass the same existence check, the loser's save throws
`PandaBear.BuildingBlocks.UniqueConstraintViolationException` (thrown by `SaveChangesTranslatingConflicts`); the behavior turns it into a
failed result instead of a 500.

- **Converts:** only `UniqueConstraintViolationException`. A rule that is mapped becomes `ResultStatus.Invalid` with one field error (the
  mapped field and message, summary `One or more validation errors occurred.`). A rule that is not mapped, or an application without a
  map, becomes `ResultStatus.Conflict` with the fixed message `A value that must be unique is already in use.`. The rule name is an
  internal identifier: it, the exception text and the inner exception are never copied into the result. Only the mapped message, which
  you write, reaches callers.
- **Does not convert:** any other exception, including `OperationCanceledException`, which propagate unchanged. A returned result,
  successful or failed, passes through untouched.
- **Does not replace a pre-check:** keep the existence check in the handler for the friendly, common-case error. This behavior only
  covers the race that the check cannot close.
- **Mapping rules:** the map is application configuration. `Add(rule, field, message)` is fluent, rejects blank arguments and rejects a
  rule that is already mapped (`ArgumentException`). Rule names are compared ordinally.

```csharp
services.AddUniqueViolationHandling(map => map
    .Add("Customer.Email", "email", "An account with this email already exists.")
    .Add("Tour.Code", "code", "A tour with this code already exists."));
```

  `AddUniqueViolationHandling(configure)` registers one `UniqueRuleMap` singleton. Calling it again merges the new rules into the same
  map, so modules can each contribute their own; mapping the same rule twice throws.
- **Registration:** the package assembly is not a mediator module, so the generator does not discover the class. Derive a sealed class in
  your own module assembly. The derived class must forward the optional map so dependency injection can supply it:

```csharp
[PipelineOrder(PipelineStage.Transaction, Order = -90)]
public sealed class UniqueBehavior<TRequest, TResponse>(UniqueRuleMap? map = null) : UniqueViolationBehavior<TRequest, TResponse>(map)
    where TRequest : IRequest<TResponse>
    where TResponse : struct, IResultLike<TResponse>;
```

  For a manually composed pipeline, `services.AddUniqueViolationHandling<TRequest, TResponse>()` registers the closed behavior; calling it
  twice for the same pair adds nothing. Without a registered map the behavior answers every violation with a conflict.
- **Ordering:** like the concurrency behavior, place it outside the transaction behavior so the transaction rolls back before the
  violation is converted. Both behaviors catch different exceptions, so their relative order does not change the outcome; keeping them
  adjacent (for example concurrency at `-100` and unique violation at `-90`) is the convention.


## AOT and Trimming

This package is marked `IsAotCompatible=true` so the SDK enables trim and Native AOT analysis for
the public runtime surface.
The repository does not keep a dedicated mediator AOT benchmark project; instead,
`SharedKernel.Mediator.PackageConsumptionTests` publishes a fresh generated consumer with
`PublishAot=true` and records publish success, trim warnings, native binary size, cold start, first
dispatch, and steady-state dispatch metrics.

## Dependencies

- [SharedKernel.Mediator.Abstractions](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.BuildingBlocks](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.BuildingBlocks/README.md) and [SharedKernel.Results](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Results/README.md)
- `Microsoft.Extensions.DependencyInjection.Abstractions`

## See Also

- [SharedKernel.Mediator.SourceGenerator](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Mediator.SourceGenerator/README.md)
- [Mediator.Sample](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/samples/Mediator/Mediator.Sample/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Mediator`. C# namespace: `PandaBear.Mediator`. Assembly/project identity: `SharedKernel.Mediator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Scoped sending

`ScopedSender` sends each request in its own service scope, disposed afterwards. Use it from components that live as long as a
connection (Blazor Server circuits) so a scoped `DbContext` is never shared between operations. It is not registered by
`AddSharedKernelMediator()`; register it with `AddScopedSender()` next to the mediator. It has a scoped lifetime (idempotent), so the
instance a component receives holds the provider of the scope it was resolved in, which for a Blazor component is the circuit scope:

```csharp
services.AddSharedKernelMediator();
services.AddScopedSender();
```

### Carrying circuit state into the operation scope

A new operation scope does not contain the circuit's scoped state. Per-connection scoped services (`AuthenticationStateProvider`, a
current-user or tenant holder, an audit context) resolve inside the handler as new, uninitialised instances. To carry that state
over, pass a scope initializer. It runs once per `Send`, on the new scope, before the handler's `ISender` is resolved, and receives
the caller's provider first and the new operation scope's provider second. If it throws, the exception propagates and the scope is
still disposed.

```csharp
services.AddScopedSender((caller, operation) =>
{
    operation.GetRequiredService<CurrentUserHolder>().Set(caller.GetRequiredService<CurrentUserHolder>().Current);
    return ValueTask.CompletedTask;
});
```

`CurrentUserHolder` stands for your own scoped state holder. Order semantics: the initializer overload always wins. It replaces any earlier `ScopedSender` registration (so a host can configure an
initializer after a shared module called the parameterless `AddScopedSender()`), a later parameterless call does not undo it, and when
several initializers are registered the last one wins.

- Prefer passing the actor or identity explicitly in the command when practical: it is clean and needs no framework support.
- When state must be reached from places that cannot take `ScopedSender`, Microsoft documents the `CircuitServicesAccessor` pattern
  (an `AsyncLocal<IServiceProvider>` filled by a `CircuitHandler`) in
  [Access server-side Blazor services from a different DI scope](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/dependency-injection).
- Idea to verify, not confirmed here: copying the authentication state by setting it on the operation scope's
  `IHostEnvironmentAuthenticationStateProvider`.
