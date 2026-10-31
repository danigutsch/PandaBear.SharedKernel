# PandaBear.SharedKernel.Mediator.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Mediator pipeline behaviors and dispatch integrations backed by Entity Framework Core.

## Usage

Register `AddEfCoreCommandTransaction<TContext, TRequest, TResponse>()` for a concrete command pipeline. `EfCoreCommandTransactionBehavior<TRequest, TResponse>` wraps the continuation using the configured DbContext transaction support. Keep request handlers and business rules in the consumer and compose only the command paths that require this transaction boundary.

## Result-aware command transactions

`ResultTransactionBehavior<TRequest, TResponse>` runs a command that returns `Result` or `Result<T>` (any `IResultLike<TResponse>`) in one retry-compatible EF Core transaction and commits only when the result is successful. A failed or invalid (`default`) result rolls back, and so does an exception, which propagates so the execution strategy can retry transient failures. Each attempt clears the change tracker, begins a read committed transaction, takes declared locks, then runs the handler. Handlers keep calling `SaveChanges`; each save gets its own savepoint inside the transaction.

The package assembly is not a mediator module, so the generator does not discover the behavior. Derive a sealed class in your own module assembly and give it the transaction stage; register a `DbContext` service (for example `services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>())`):

```csharp
[PipelineOrder(PipelineStage.Transaction)]
public sealed class TransactionBehavior<TRequest, TResponse>(DbContext dbContext, IServiceScopeFactory scopeFactory, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : ResultTransactionBehavior<TRequest, TResponse>(dbContext, scopeFactory, logger)
    where TRequest : ICommand<TResponse>
    where TResponse : struct, IResultLike<TResponse>;
```

- **Locks.** A command implements `ITransactionLockDeclaration` and exposes its advisory lock names as `TransactionLocks`; the declaration holds no persistence type, so the application layer needs no EF Core reference. After the transaction begins, the behavior takes them at tier 0 through one new `PostgreSqlOrderedTransactionLocks` per attempt, so retries never reuse an instance.
- **Hooks.** `IAfterRollback<TRequest, TResponse>` runs once after the last attempt when the result was not successful (not for exceptions); `IAfterCommit<TRequest, TResponse>` runs once after a commit. Both resolve from a fresh scope outside the transaction. A hook failure is logged and never changes the response. Put records that must outlive a rollback, such as rejection audits, in an after-rollback hook.
- **Joining.** When a transaction is already active, the behavior joins it, takes no locks and runs no hooks. It throws `InvalidOperationException` if that transaction runs at repeatable read, serializable or snapshot isolation, because checks that read state after taking locks need a fresh snapshot per statement.
- **Non-relational providers** run the handler directly.

### Commit ambiguity and retries

The execution strategy replays the whole attempt after a transient failure. When the connection drops while the transaction
commits, the outcome is unknown and the strategy retries as if the transaction had been rolled back. Commands that create rows
must therefore use client-generated keys (for example GUIDs) so a replay conflicts instead of silently duplicating, or verify
the outcome with `IExecutionStrategy.ExecuteInTransaction`. See the
[EF Core connection resiliency guide](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue).
Hooks run once after the last attempt, outside the transaction and the strategy, so they never repeat on a retry.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Mediator.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Mediator;
using PandaBear.Mediator.EntityFrameworkCore;

public sealed record SaveItem : ICommand<int>;
public sealed class AppDbContext : DbContext;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddEfCoreCommandTransaction<AppDbContext, SaveItem, int>();
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Mediator.EntityFrameworkCore`. C# namespace: `PandaBear.Mediator.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Mediator.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
