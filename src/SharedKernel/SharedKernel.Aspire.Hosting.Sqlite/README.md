# SharedKernel.Aspire.Hosting.Sqlite

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Aspire hosting integration that models a SQLite database file as a resource. No container and no third-party package.

## Usage

```csharp
var db = builder.AddSqlite("app-db", "data/app.db");

builder.AddProject<Projects.Web>("web")
       .WithReference(db, connectionName: "Default")
       .WaitFor(db);
```

The project receives `ConnectionStrings__Default=Data Source=<absolute path>`. A relative path is resolved against the AppHost
directory. A path that contains a semicolon is quoted in the connection string.

`AddSqlite` creates the database file's parent directory, because SQLite creates the file but not its folder. It does not create the
database file. The dashboard shows the resource as `Sqlite` in the `Running` state with a `DatabasePath` property.

## Health check

The resource has a health check named `{name}-file`. It is healthy when the database directory exists and the
file, if it exists, can be opened for reading. The check is read-only: it creates, modifies and deletes nothing, and it reports that
the location exists and the file is readable, not that it is writable. Its messages never include the path.

## Limits

No migrations, no web UI, no container, no backup. Apply the schema from the consuming application.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Hosting.Sqlite`. Assembly/project identity: `SharedKernel.Aspire.Hosting.Sqlite`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
