# PandaBear.SharedKernel.DispositionHold.EntityFrameworkCore

PostgreSQL store and fail-closed guard for `PandaBear.SharedKernel.DispositionHold.Abstractions`.

- `AddDispositionHolds<TContext>()` registers `IDispositionHoldStore` and `IDispositionHoldGuard` and adds the hold
  table to the context through `IDbContextConfiguration<TContext>`. The default table is `disposition_hold.holds`.
- Both operations need a transaction the caller has already opened on the context. Without one they throw
  `DispositionHoldStateUnknownException`, so a purge refuses instead of running unprotected.
- The transaction must run at read committed. Repeatable read and serializable take their snapshot at the first
  statement, which can predate a hold committed while the lock was awaited, so both operations refuse them.
- Save the hold and commit in the same transaction that called `Add`; the lock does not outlive it.
- `Add` and `Check` take PostgreSQL transaction-scoped advisory locks per scope. A hold placed while a purge checks is
  either seen by that purge or waits for it. The locks release at commit or rollback.
- Lock order: take the locks your command declares first, then call the guard or `Add`. Hold locks are the last tier.
  Locks go through `PandaBear.Npgsql.PostgreSqlOrderedTransactionLocks` at tier 1, in ascending key order, with a new
  instance per call so a lock released by a savepoint rollback is taken again. Call `Check` once with all scopes of a purge.
- Retention periods, legal bases and who may place a hold stay in the host.
