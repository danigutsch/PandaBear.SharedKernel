# PandaBear.SharedKernel.Idempotency.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Entity Framework Core persistence provider for reusable idempotency stores.

## Usage

Register `AddIdempotencyStore<TContext>()` with the owning DbContext and optionally configure `IdempotencyStorageOptions`. The package persists reusable idempotency state through EF Core. The application owns provider selection, schema/table policy, migrations, request scope, and the surrounding business operation.

## Retention owner and purge

`TryStart` can record a nullable, indexed `RetentionOwner` (opaque, at most 128 characters) in a new column of the
idempotency table. The column is additive and nullable: existing callers and rows are unaffected, but the host owns
the migration that adds the column and index.

`AddIdempotencyStore<TContext>()` also registers `IIdempotencyRetentionStore`. `PurgeExpired` deletes one batch of at
most `batchSize` entries whose last touch (completion time, else start time) is before the cutoff, oldest first with
scope and key as the tie-break. It selects the batch with `FOR UPDATE SKIP LOCKED`, so it skips rows locked by another
worker, and deletes exactly the selected rows in the same transaction (the transaction of the context, or one of its own). Entries whose owner is in the held set, and unattributed entries in the owner-tracked scope
prefix, are retained and counted; they do not use up the batch. Call it inside the transaction in which the host
checks its holds. A host that shares the table between features passes `scopePrefix` (for example `documents.`) to
limit the purge to its own scopes: entries outside the prefix are never read, counted or deleted, and an empty or
whitespace prefix is rejected. Without `scopePrefix` the purge covers the whole table. It requires the Npgsql EF Core provider. Retention periods, the tracked scopes, the held owners and purge evidence
stay in the host.

The overload that takes a `guard` (`Func<IReadOnlyCollection<string>, CancellationToken, ValueTask<IReadOnlySet<string>>>`)
lets the host check its holds against exactly the rows about to be deleted. `heldRetentionOwners` stays a pre-filter. The
store calls the guard once per batch, after selecting and locking the rows and before deleting, with the distinct non-null
owners of the selected rows (not at all when there are none). The guard returns the owners to keep, and the store deletes
only the selected rows whose owner is not returned. PostgreSQL READ COMMITTED gives each statement its own snapshot, so a
hold committed after a purge statement began is invisible to that statement; the guard runs in the same transaction under
the host's own locks (for example `PandaBear.SharedKernel.DispositionHold`). If the guard throws or its token is
cancelled, nothing is deleted and the exception propagates. `Held` counts the expired entries kept because their owner is
in `heldRetentionOwners` plus the selected entries kept because the guard returned their owner. Unattributed entries are
retained and counted as before and never reach the guard. The overload without a guard shares the same code path.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Idempotency.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Idempotency.EntityFrameworkCore;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddIdempotencyStore<AppDbContext>();
}

public sealed class AppDbContext : DbContext;
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Idempotency.EntityFrameworkCore`. C# namespace: `PandaBear.Idempotency.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Idempotency.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
