# PostgreSQL test support

Package ID: `PandaBear.SharedKernel.Npgsql.Testing`. Namespace: `PandaBear.Npgsql.Testing`.
Packages remain preview and are consumed from a validated local candidate; registry publication is deferred.

`PostgreSqlTestDatabasePool` leases independent databases on a caller-owned PostgreSQL server. It starts
no containers and owns no application migrations. Dispose each lease after its scenario, then dispose the
pool before stopping the server. The pool retries outstanding cleanup without affecting sibling pools.

```csharp
await using var pool = new PostgreSqlTestDatabasePool(serverConnectionString);
await using var database = await pool.Create(cancellationToken);
var connectionString = database.ConnectionString;
// Run the consumer's migrations and scenario using this isolated connection string.
```

The connection must permit database creation. Names are generated internally. Cleanup uses a bounded,
cancellation-independent connection and forced database removal; failures retain ownership for the pool's
final cleanup. The lease owns `DataSource`; additional sources returned by `CreateDataSource` belong to
the caller and should be disposed before the lease. Never use production database credentials here.

Existing native xUnit assembly-fixture support remains available for library test suites. Its local
`CreateBuilder` override must remain in the executable test assembly with `NoInlining` because Aspire
uses the call stack to find generated DCP metadata. Consumers sharing several infrastructure services
can instead hold a database pool in their own native xUnit assembly fixture.

The package uses the existing Npgsql, Aspire PostgreSQL, Aspire testing and xUnit core dependencies.
Application authentication, browser setup, resource images, reset policy and business assertions remain
consumer-owned. No custom runner or discovery framework is required.
