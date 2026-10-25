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

## Who owns the server

`PostgreSqlTestServerFixture` is the native xUnit assembly fixture for library test suites. Creating and
initializing it starts nothing, so a test assembly whose selected tests need no database pays for no container.
The first test that needs a database resolves the server, either by borrowing it or by starting one:

- Borrow: when the environment variable `SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING` is set, its value is
  the connection string of a server that another process owns. The fixture only creates and drops its own
  isolated databases on it and never stops it. A value that is not a PostgreSQL connection string with a host
  fails the test that needs a database, with a message that names the variable and never repeats its value. A
  server that does not accept a connection fails it too: the first test that needs a database connects once and
  the failure is cached, so a server that accepts connections and never answers costs the assembly one connection
  timeout, 15 seconds unless the connection string says otherwise, not one per test. A set variable never falls
  back to a container of its own, because that would hide a broken owner and start a second server.
- Own: when the variable is not set or is blank, the fixture starts a PostgreSQL container of its own through
  its `CreateBuilder` override on that first demand and stops it when the assembly is done. The start is shared
  by concurrent tests and a failed start is cached, so it fails every test that needs a database instead of
  repeating a long startup timeout.

The `CreateBuilder` override must remain in the executable test assembly with `NoInlining`, because Aspire uses
the call stack to find generated DCP metadata; the fixture calls it only when it starts a server of its own.

Several assemblies can borrow one server. Whoever runs the tests starts a PostgreSQL server, exports its
connection string in the variable for the test processes, and removes the server after the run. This
repository does that in CI for its three database test assemblies with a small host project that is not part
of the package (see `tests/README.md` in the repository).

An application that owns its test infrastructure can reuse the building blocks instead of an assembly fixture
of its own: `OnDemandResource<T>` starts the infrastructure on first use and shares it among concurrent tests,
and a `PostgreSqlTestDatabasePool` built with the resolver constructor leases isolated databases from the
server that resource provides, without starting anything until a test creates its first database.

## Why not named persistent containers

Several test processes cannot share one container through `WithContainerName` and
`ContainerLifetime.Persistent` with the defaults of this package. This was checked with Aspire 13.6.1 and DCP
0.26.5, in AppHost projects without user secrets.

- Aspire reuses a persistent container only when its lifecycle key matches, and the key includes the host port
  and the environment, including the generated password. Every test process draws its own random proxyless port
  and password, so a later process stops, removes and recreates the shared container under the earlier one,
  which fails with connection errors 57P01, 57P03 and 28P01.
- DCP never removes a persistent container, so something outside the test processes would have to remove it
  after the run.
- It worked only when an external run owner fixed the name, the port and the password for the whole run. The
  owner host does that without relying on key equality: one process owns the server and the others borrow its
  connection string. Aspire can also keep a generated password and public port of a persistent resource in
  user secrets; that way was not tried.

The package uses the existing Npgsql, Aspire PostgreSQL, Aspire testing and xUnit core dependencies.
Application authentication, browser setup, resource images, reset policy and business assertions remain
consumer-owned. No custom runner or discovery framework is required.
