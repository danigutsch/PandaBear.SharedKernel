# SharedKernel.Npgsql

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Small raw-Npgsql primitives with a stable, provider-specific boundary.

## Contract

- `PostgreSqlTransactionAdvisoryLock` executes parameterized `pg_advisory_xact_lock` SQL on a
  caller-supplied active `NpgsqlConnection` and `NpgsqlTransaction`.
- `PostgreSqlSessionAdvisoryLock` executes parameterized `pg_advisory_lock` SQL on a dedicated
  `NpgsqlConnection` opened from a caller-supplied `NpgsqlDataSource`, returning an
  `IAsyncDisposable` lease.
- `ConfigureTracingWithoutFirstResponseEvent` keeps Npgsql tracing enabled while omitting the
  optional first-response event that can carry provider-owned payload data.
- PostgreSQL releases the acquired lock when the caller's transaction commits, rolls back, or is
  disposed. Callers must retain the transaction for their entire critical section.
- PostgreSQL releases a session lock when its lease is disposed and the owning connection closes.
  Callers must retain the lease for their entire critical section.
- Callers own lock-key derivation, command timeout configuration, retry behavior, and cancellation
  policy.
- The package does not own database schemas, migrations, connection-string configuration,
  distributed-cache behavior, or application-specific authentication/token rules.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Npgsql`. C# namespace: `PandaBear.Npgsql`. Assembly/project identity: `SharedKernel.Npgsql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
