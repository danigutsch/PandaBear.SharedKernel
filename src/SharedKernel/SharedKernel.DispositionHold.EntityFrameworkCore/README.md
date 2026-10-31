# PandaBear.SharedKernel.DispositionHold.EntityFrameworkCore

PostgreSQL store and fail-closed guard for `PandaBear.SharedKernel.DispositionHold.Abstractions`.

- `AddDispositionHolds<TContext>()` registers `IDispositionHoldStore` and `IDispositionHoldGuard` and adds the hold
  table to the context through `IDbContextConfiguration<TContext>`. The default table is `disposition_hold.holds`.
- Both operations need a transaction the caller has already opened on the context. Without one they throw
  `DispositionHoldStateUnknownException`, so a purge refuses instead of running unprotected.
- `Add` and `Check` take PostgreSQL transaction-scoped advisory locks per scope. A hold placed while a purge checks is
  either seen by that purge or waits for it. The locks release at commit or rollback.
- Lock order: take the locks your command declares first, then call the guard or `Add`. Hold locks are the last tier.
  Within it, scopes are locked in ascending key order, so overlapping scope sets cannot deadlock each other.
- Retention periods, legal bases and who may place a hold stay in the host.
