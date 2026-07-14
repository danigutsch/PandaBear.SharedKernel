# SharedKernel.Observability.Npgsql

Reusable, read-only PostgreSQL index-health collection for .NET services.

## Contract

- Use a dedicated `NpgsqlDataSource` built with a least-privilege monitoring role. Grant only
  `pg_read_all_stats` when collection must observe all database objects.
- `PostgreSqlIndexHealthCollector` runs fixed catalog `SELECT` statements only. It never creates,
  changes, deletes, or maintains an index.
- PostgreSQL object names are returned only as in-memory advisory evidence. They must never become
  metric tags or structured log fields.
- The `SharedKernel.Observability.Npgsql` meter emits only finite `outcome`, `action`, and `reason`
  dimensions. It emits no SQL, parameter, connection-string, role, tenant, schema, table, or index
  values.
- Permission, capability, connectivity, and timeout failures become bounded collection outcomes.
  Cooperative cancellation is rethrown.

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
