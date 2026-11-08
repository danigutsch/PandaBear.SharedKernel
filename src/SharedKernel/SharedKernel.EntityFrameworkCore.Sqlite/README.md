# PandaBear.SharedKernel.EntityFrameworkCore.Sqlite

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

SQLite support for the shared-kernel Entity Framework Core primitives. It currently provides the SQLite
`IUniqueViolationParser`, which lets `DbContext.SaveChangesTranslatingUniqueViolations` report a unique-index
violation by application rule. The provider-neutral pieces (`HasUniqueRule`, `UniqueConstraintViolationException`) are
documented with [SharedKernel.EntityFrameworkCore](../SharedKernel.EntityFrameworkCore/README.md).

SQLite names the violated columns (`UNIQUE constraint failed: Customers.Email`), not a constraint, so the parser reports
the table and columns, and an index name for expression indexes. Primary-key violations are recognized the same way.

```csharp
services.AddSqliteUniqueViolationParser();   // registers IUniqueViolationParser
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EntityFrameworkCore.Sqlite`. C# namespace: `PandaBear.EntityFrameworkCore.Sqlite`.
Assembly/project identity: `SharedKernel.EntityFrameworkCore.Sqlite`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
