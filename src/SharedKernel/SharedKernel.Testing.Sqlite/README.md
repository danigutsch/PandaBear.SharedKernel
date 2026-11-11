# SharedKernel.Testing.Sqlite

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

A private in-memory SQLite database per test.

```csharp
using PandaBear.Testing.Sqlite;

using var database = new SqliteInMemoryDatabase("orders");
services.AddDbContext<OrdersContext>(options => options.UseSqlite(database.ConnectionString));
```

- `ConnectionString` is `Data Source=file:{name}-{guid}?mode=memory&cache=shared`: every connection opened with it sees the same database,
  and no other test does.
- The object holds one connection open, which keeps the database alive between the short-lived connections of the code under test.
- `Dispose` closes that connection and clears the pool of this connection string (`SqliteConnection.ClearPool`), so no idle pooled
  connection keeps the database and its memory alive after the test. Only this database's pool is cleared, not every pool of the
  process (`ClearAllPools`), so parallel tests are not disturbed.
- The name is a readable prefix: letters, digits and dashes.

The package references `Microsoft.Data.Sqlite.Core` only; the test project supplies the native SQLite library, for example through
`Microsoft.EntityFrameworkCore.Sqlite` or `Microsoft.Data.Sqlite`. For SQLite files in a `TestWorkspace` of
SharedKernel.IntegrationTesting, pass `SqliteConnection.ClearAllPools` as its `releaseFiles` callback.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Sqlite`. C# namespace: `PandaBear.Testing.Sqlite`. Assembly/project identity: `SharedKernel.Testing.Sqlite`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
