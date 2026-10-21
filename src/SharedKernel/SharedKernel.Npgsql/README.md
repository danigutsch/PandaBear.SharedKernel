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
- `PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian` derives a signed 64-bit key from the first
  eight SHA-256 bytes of the exact UTF-8 identity, interpreted in big-endian order.
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

## Lock-key derivation

```csharp
using PandaBear.Npgsql;

long lockKey = PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian("inventory:123");
```

Pass the key to either advisory-lock acquisition method. Keep business-specific identity composition
in the consumer; this helper does not change existing callers or their lock keys.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Npgsql`. C# namespace: `PandaBear.Npgsql`. Assembly/project identity: `SharedKernel.Npgsql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
