# PandaBear.SharedKernel.EntityFrameworkCore.Npgsql

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

PostgreSQL support for the shared-kernel Entity Framework Core primitives. It currently provides the PostgreSQL
`IUniqueViolationParser`, which lets `DbContext.SaveChangesTranslatingUniqueViolations` (and `SaveChangesTranslatingConflicts`) report a
unique-index violation by application rule. The provider-neutral pieces (`HasUniqueRule`, `UniqueConstraintViolationException`) are
documented with [SharedKernel.EntityFrameworkCore](../SharedKernel.EntityFrameworkCore/README.md).

The parser recognizes a `PostgresException` with SQLSTATE `23505` (`PostgresErrorCodes.UniqueViolation`) anywhere in the inner-exception
chain of the `DbUpdateException`. PostgreSQL reports the violated constraint, which for a unique index is the index name, and its table;
the rule is found by that name. Primary-key violations are recognized the same way. The columns are only in the error detail, which Npgsql
hides unless `Include Error Detail` is set, so they are not read. Other errors, such as foreign-key, not-null or exclusion violations,
are not unique violations.

```csharp
services.AddNpgsqlUniqueViolationParser();   // registers IUniqueViolationParser
```

The package references `Npgsql` only, not the EF Core PostgreSQL provider, so it fits any provider version that uses that Npgsql.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EntityFrameworkCore.Npgsql`. C# namespace: `SharedKernel.EntityFrameworkCore.Npgsql`.
Assembly/project identity: `SharedKernel.EntityFrameworkCore.Npgsql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
