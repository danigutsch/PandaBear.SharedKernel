# SharedKernel.Observability.Npgsql

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable, read-only PostgreSQL index-health collection for .NET services.

## Contract

- Use a dedicated `NpgsqlDataSource` built with a least-privilege monitoring role. Grant only
  `pg_read_all_stats` when collection must observe all database objects.
- `PostgreSqlIndexHealthCollector` runs fixed catalog `SELECT` statements only. It never creates,
  changes, deletes, or maintains an index.
- PostgreSQL object names are returned only as in-memory advisory evidence. They must never become
  metric tags or structured log fields.
- The `SharedKernel.Observability.Npgsql` meter emits `sharedkernel.index_health.collections` and
  `sharedkernel.index_health.assessments` with only the finite `sharedkernel.index_health.outcome`,
  `sharedkernel.index_health.action`, and `sharedkernel.index_health.reason` dimensions, plus
  `db.system.name` = `postgresql`. It emits no SQL, parameter, connection-string, role, tenant,
  schema, table, or index values.
- Hosted monitoring assigns native Npgsql pool names by source order in the registration
  (`postgresql-index-health-1`, `postgresql-index-health-2`, and so on).
- Permission, capability, connectivity, and timeout failures become bounded collection outcomes.
  Cooperative cancellation is rethrown.

## PostgreSQL versions

The index-health collector supports PostgreSQL major versions 16 through 18. It reads the
server version from the dedicated monitoring connection before running catalog queries. A server
outside that range produces an empty `Unsupported` result and one bounded collection metric.
The compatibility check runs only when the advisory collector opens its monitoring connection.

The read-only catalog and monitoring-role checks passed at the floor, PostgreSQL 16.16, and on
the consuming application's PostgreSQL 18.4. Both runs used digest-pinned official images. A
server reporting an unsupported catalog capability also produces the bounded `unsupported`
outcome. Connection and timeout failures produce `unavailable`.

## Recommendation policy

The policy is intentionally conservative. A candidate must have a PostgreSQL statistics period of
at least seven days, current table statistics, and an estimated 10,000 rows. Protected, invalid,
partial, and expression indexes never receive removal or modification recommendations. Every
recommendation is advisory and requires human review before a separately managed migration changes
database schema.

## References

- [Npgsql data sources](https://www.npgsql.org/doc/basic-usage.html#data-source)
- [Npgsql metrics](https://www.npgsql.org/doc/diagnostics/metrics.html)
- [PostgreSQL statistics views](https://www.postgresql.org/docs/16/monitoring-stats.html)
- [PostgreSQL predefined roles](https://www.postgresql.org/docs/16/predefined-roles.html)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Observability.Npgsql`. C# namespace: `SharedKernel.Observability.Npgsql`. Assembly/project identity: `SharedKernel.Observability.Npgsql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
