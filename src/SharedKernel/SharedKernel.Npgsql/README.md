# SharedKernel.Npgsql

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Small raw-Npgsql primitives with a stable, provider-specific boundary.

## Contract

- `PostgreSqlTransactionAdvisoryLock` executes parameterized `pg_advisory_xact_lock` SQL on a
  caller-supplied active `NpgsqlConnection` and `NpgsqlTransaction`.
- `PostgreSqlOrderedTransactionLocks` takes several transaction-scoped advisory locks for one
  open `NpgsqlConnection` and `NpgsqlTransaction` in a single global order, so transactions that
  need more than one lock cannot deadlock on them. Callers pass `PostgreSqlTransactionLockRequest`
  values (`Tier`, `Name`); keys come from `PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(Name)`.
- `PostgreSqlSessionAdvisoryLock` executes parameterized `pg_advisory_lock` SQL on a dedicated
  `NpgsqlConnection` opened from a caller-supplied `NpgsqlDataSource`, returning an
  `IAsyncDisposable` lease.
- `PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian` derives a signed 64-bit key from the first
  eight SHA-256 bytes of the exact UTF-8 identity, interpreted in big-endian order.
- `PostgreSqlArtifactIntegrity` reports the integrity of one bounded page of separately stored PostgreSQL artifacts.
- `ConfigureTracingWithoutFirstResponseEvent` keeps Npgsql tracing enabled while omitting the
  optional first-response event that can carry provider-owned payload data.
- PostgreSQL releases the acquired lock when the caller's transaction commits, rolls back, or is
  disposed. Callers must retain the transaction for their entire critical section.
- PostgreSQL releases a session lock when its lease is disposed and the owning connection closes.
  Callers must retain the lease for their entire critical section.
- Callers own lock identities, prefixes, formatting, validation, command timeouts, retries, and
  cancellation policy. Key derivation does not trim or normalize text; empty identities are supported
  and null is rejected. Preserve existing key algorithms and byte order when coordinating with other
  callers. A 64-bit hash does not guarantee collision-free keys.
- The package does not own database schemas, migrations, connection-string configuration,
  distributed-cache behavior, or application-specific authentication/token rules.

## Scheduling observations

`PostgreSqlScheduleRunStore` records run observations and reads up to 100 entries at a time for an
exact configured job. The cursor orders by start timestamp and PostgreSQL UUID order. The returned
record contains an opaque run identifier, dates, and status. The query projects only the columns it
uses. A metadata view can expose the same columns to a separate read data source.
Use `ReadActivePage` to list unfinished runs whose active observation is still fresh. It applies
the freshness filter before paging, so newer completed or expired runs do not displace active
observations. Reuse its cursor within the active list for the same job. Freshness is evaluated on
each read, so an observation can expire or finish before the next page.

The consumer owns migration and cleanup of the observation table. For example:

```sql
CREATE TABLE operations.schedule_runs (
    job_name text COLLATE pg_catalog."C" NOT NULL,
    run_id uuid PRIMARY KEY,
    started_at timestamptz NOT NULL,
    scheduled_for timestamptz,
    observed_until timestamptz NOT NULL,
    finished_at timestamptz,
    outcome integer,
    retry_at timestamptz,
    CHECK (observed_until >= started_at),
    CHECK (
        (finished_at IS NULL AND outcome IS NULL AND retry_at IS NULL)
        OR (finished_at >= started_at AND finished_at IS NOT NULL AND outcome IS NOT NULL
            AND outcome BETWEEN 2 AND 6 AND ((outcome = 5) = (retry_at IS NOT NULL)))
    )
);

CREATE INDEX schedule_runs_job_recent
    ON operations.schedule_runs (job_name, started_at DESC, run_id DESC);
```

For frequent active-page reads on a table with retained completed history, an optional partial
index lets PostgreSQL read unfinished observations in page order. Freshness is evaluated in the
query using the database clock.

```sql
CREATE INDEX schedule_runs_unfinished_recent
    ON operations.schedule_runs (job_name, started_at DESC, run_id DESC)
    WHERE outcome IS NULL;
```

- Pass the registered job name and a stable UUID to `Start`. Reuse the UUID after an uncertain
  write. Each independently observed run has its own UUID, including concurrent runs across hosts.
- A fresh unfinished report appears as `Running`. Call `Refresh` after confirming that the run is
  still active. An uncompleted report becomes `Unknown` at expiry, including after a process loss.
  A report can remain fresh until expiry even if the worker has stopped. The provider derives times
  and expiry from the PostgreSQL clock. Use existing execution locks and coordination separately.
- `Complete` records the first known completion. `Failed` describes a failure with retry and
  terminal state unspecified. Use `TerminalFailure` or `RetryScheduled` when the execution provider
  confirms that outcome. A retry requires its confirmed time.
- The host's observation policy controls where to record starts and completions, how to handle
  persistence errors, and whether to use a separate bounded completion token during shutdown.
  The fixed-delay polling service remains responsible for draining batches and waiting between
  cycles. Its telemetry shows local activity and completed cycles. Supply an intended fire time
  only when an execution provider knows it. The store reports observed runs across host restarts.

## Lock-key derivation

```csharp
using PandaBear.Npgsql;

var lockKey = PostgreSqlLockKey.FromIdentity("inventory:123");
```

`PostgreSqlLockKey` carries the signed numeric identity with value equality and signed ordering.
Every `long`, including zero, is valid; use its constructor for an existing numeric key. Identity
hashing retains the exact existing protocol. The original static derivation helper and `long`
acquisition overloads remain available.

Pass the typed key to either advisory-lock `AcquireKey` method. Keep business-specific identity composition
in the consumer; this helper does not change existing callers or their lock keys.

## Ordered transaction locks

```csharp
using PandaBear.Npgsql;

var locks = new PostgreSqlOrderedTransactionLocks(connection, transaction);
await locks.Acquire([new(0, $"admin:tour-capacity:{tourId}")], ct);
await locks.Acquire([new(1, $"disposition-hold:{scope}")], ct);
```

- Locks requested together are deduplicated by key (the lowest tier wins) and taken one statement at a
  time, sorted by tier, then numeric key, then ordinal name. A lock is taken again even when the
  instance took it before, because PostgreSQL releases a lock taken after a savepoint when the
  transaction rolls back to that savepoint; a lock the instance took before is exempt from the order
  check.
- The instance remembers the last lock taken. A later request that sorts before it, in a lower tier or
  earlier within the same tier, throws `InvalidOperationException` naming both locks, before any lock
  of that request is taken. Request every lock of a tier in one call when possible.
- Create one instance per transaction attempt and share it with all code that takes locks in that
  transaction. After rolling back to a savepoint created before locks were taken, treat those locks
  as released and use a new instance if the order must be enforced again. Separate instances over the
  same transaction do not know each other's locks. Do not
  reuse an instance for the next transaction (for example on a retry): Npgsql reuses its transaction
  object per connection.
- The type never begins, commits, rolls back or disposes the transaction, and it throws when the
  transaction is no longer open. Assign tiers by convention in the consuming repository, for example
  tier 0 for locks a command declares and tier 1 for guards taken later.

## Binary artifact integrity

Use `PostgreSqlArtifactIntegrity.ReadPage` for an operator report over revisions with a separate body relation.
Provide each schema, relation, and column as an identifier in `PostgreSqlArtifactIntegrityMapping`. The metadata
ID and body ID must be unique UUID keys. The metadata ID must be non-null. A populated eligibility column
selects the metadata rows to check. Supply the columns holding the nullable recorded byte length and lowercase
hexadecimal SHA-256 digest. The body content column uses PostgreSQL `bytea`.

Each call opens a read-only transaction with a 30-second statement timeout. The page size ranges from 1 to 100.
The query selects up to that many eligible metadata rows in PostgreSQL UUID order before looking up their bodies.
Start with a null cursor, pass the last reported ID as the next exclusive cursor, and stop when a page is empty. Each row returns
an opaque UUID and a typed status. PostgreSQL checks for missing body bytes, missing integrity metadata,
a length mismatch, and a digest mismatch in that order. The digest comparison is case-sensitive and uses the
stored bytes. Only the outcome and identifier are returned to the caller.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Npgsql`. C# namespace: `PandaBear.Npgsql`. Assembly/project identity: `SharedKernel.Npgsql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
