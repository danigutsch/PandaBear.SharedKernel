# SharedKernel.Npgsql

Small raw-Npgsql primitives with a stable, provider-specific boundary.

## Contract

- `PostgreSqlTransactionAdvisoryLock` executes parameterized `pg_advisory_xact_lock` SQL on a
  caller-supplied active `NpgsqlConnection` and `NpgsqlTransaction`.
- PostgreSQL releases the acquired lock when the caller's transaction commits, rolls back, or is
  disposed. Callers must retain the transaction for their entire critical section.
- Callers own lock-key derivation, command timeout configuration, retry behavior, and cancellation
  policy.
- The package does not own database schemas, migrations, connection-string configuration,
  distributed-cache behavior, or application-specific authentication/token rules.
