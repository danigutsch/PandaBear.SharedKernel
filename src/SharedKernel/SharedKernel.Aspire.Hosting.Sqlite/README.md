# SharedKernel.Aspire.Hosting.Sqlite

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration that models a SQLite database file as a resource. No container and no third-party package.

## Usage

```csharp
var db = builder.AddSqlite("app-db");

builder.AddProject<Projects.Web>("web")
       .WithReference(db, connectionName: "Default")
       .WaitFor(db);
```

The project receives `ConnectionStrings__Default=Data Source=<data directory>/app-db.db`. The database file is always named after
the resource, `{name}.db`. A path that contains a semicolon is quoted in the connection string.

## Ephemeral by default

Like an Aspire container without a data volume, `AddSqlite(name)` keeps nothing between runs:

- The database lives in a fresh directory under the OS temp path, `aspire-sqlite-{name}-{random}`, unique to the AppHost run and
  the resource.
- `AddSqlite` has no file system side effect. The directory is created when the application starts (the `BeforeStartEvent`), with
  owner-only permissions on Unix because it holds application data. SQLite creates the database file on first use.
- When the AppHost stops, the directory and everything in it are deleted. Deletion is best effort: a failure is logged as a warning
  with the path. A crashed or killed AppHost cannot clean up, so its directory stays in the temp folder (`/tmp` or `$TMPDIR` on
  Linux, `%TEMP%` on Windows) until it is deleted by hand or by the operating system's temp cleanup.
- Publishing (`--publisher`) creates and deletes nothing. The manifest still carries the computed temp path in connection strings,
  so a deployment must provide its own connection string.

### Keeping the data: `WithDataBindMount`

Persistence is opt-in. `WithDataBindMount` places the database in a host folder that you own:

```csharp
var db = builder.AddSqlite("app-db")
                .WithDataBindMount("../../.local/data");   // relative to the AppHost directory
```

The database is then `<folder>/app-db.db`. The folder is created at start (never when publishing) and never deleted. The temporary
directory is not created at all. Connection strings, the health check and the dashboard follow the folder, even for references added
before the call.

The name mirrors Aspire's container helper `WithDataBindMount`, which also maps data to a host folder. `WithDataVolume` and
`WithPersistentLifetime` do not exist here: there is no container, so there is no runtime-managed volume and no container to reuse
across runs.

To let each developer choose, `WithDataBindMountFromConfiguration` reads the folder from an AppHost setting and keeps the database
ephemeral when the setting is missing or blank, so test runs and fresh clones stay isolated:

```csharp
var db = builder.AddSqlite("app-db")
                .WithDataBindMountFromConfiguration("AppHost:DataFolder");   // e.g. --AppHost:DataFolder=./.local/data
```

### Files next to the database

`db.Resource.DataDirectory` is the folder that holds the database. Other files that belong with it, such as uploaded media, can live
there and share its lifetime: deleted on stop when ephemeral, kept when bind-mounted. Pass it to a project with a callback, so it is
read when the environment is built and a later `WithDataBindMount` is honored:

```csharp
builder.AddProject<Projects.Web>("web")
       .WithReference(db, connectionName: "Default")
       .WithEnvironment(context => context.EnvironmentVariables["Media__Path"] = Path.Combine(db.Resource.DataDirectory, "media"))
       .WaitFor(db);
```

The application creates the subfolder itself.

## Read-only consumers

A second project that must only read the same file, such as a public site next to an admin host that owns the schema, uses
`WithReadOnlyReference`:

```csharp
var admin = builder.AddProject<Projects.Admin>("admin").WithReference(db, connectionName: "Default").WaitFor(db);

builder.AddProject<Projects.Public>("public")
       .WithReadOnlyReference(db, connectionName: "Default")
       .WaitFor(admin);
```

It receives `ConnectionStrings__Default=Data Source=<path>;Mode=ReadOnly` (the database resource's name when no connection
name is given) and shows the reference on the dashboard. SQLite then refuses every write on that connection. The mode travels in
configuration, so the reading application should still force `Mode=ReadOnly` on the connection string it receives.

The dashboard shows the resource as `Sqlite` in the `Running` state with a `DatabasePath` property.

## Health check

The resource has a health check named `{name}-file`. It is healthy when the database directory exists and the
file, if it exists, can be opened for reading, so `WaitFor` waits for exactly that. The check is read-only: it creates, modifies and
deletes nothing. It does not test writability, which cannot be done without writing; a read-only volume or wrong permissions surface
as a SQLite "readonly database" error at first use. Its messages never include the path.

## Limits

No migrations, no web UI, no container, no backup. Apply the schema from the consuming application.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.Sqlite`. Assembly/project identity: `SharedKernel.Aspire.Hosting.Sqlite`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
