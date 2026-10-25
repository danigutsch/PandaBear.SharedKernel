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

When another component starts the server on demand, pass a resolver instead of a connection string. The
pool calls it on the first `Create`, inside its gate and with that caller's token, and keeps the first
successful result. A failed or cancelled resolution registers no database, so the next `Create` resolves
again. A pool that never created a database never calls the resolver: disposing it neither resolves nor
contacts a server. A resolver that returns `null` or a blank string is a defect, not a retry signal: `Create`
fails with `InvalidOperationException` and registers nothing, so a resolver whose server is not ready yet must wait
or throw. Other resolver failures reach the caller of `Create` unchanged. `OnDemandResource<T>` from
`PandaBear.SharedKernel.IntegrationTesting` is a natural source.

```csharp
await using var server = new OnDemandResource<AspireTestApplication>(startServer);
await using var pool = new PostgreSqlTestDatabasePool(async ct =>
    await (await server.Get(ct)).GetConnectionString("postgres", ct));
await using var database = await pool.Create(cancellationToken);
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
