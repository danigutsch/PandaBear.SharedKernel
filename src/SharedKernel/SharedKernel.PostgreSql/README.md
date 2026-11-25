# PandaBear.SharedKernel.PostgreSql

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Provider-free PostgreSQL rules. The package has no dependency, so a provider-agnostic Entity Framework Core package and a raw Npgsql
package can both use the same rule without taking the other's dependencies. See the
[package decision](../../../docs/adr/20261125-postgresql-package.md).

## Unquoted identifiers

`PostgreSqlIdentifier.IsUnquoted` checks a name that a library writes into SQL text, such as a configured schema or table name:
an ASCII letter or `_`, then ASCII letters, digits, or `_`, at most 63 bytes. PostgreSQL silently truncates a longer name to
NAMEDATALEN-1 (63) bytes, so two long names could select the same object. Only ASCII is accepted, so the character count is the
byte count. The value is not lowercased; PostgreSQL folds an unquoted name to lower case. The idempotency, integration event, and
disposition hold storage options and the event-sourcing schema use it.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version.

```text
dotnet add package PandaBear.SharedKernel.PostgreSql --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using SharedKernel.PostgreSql;

public static class Example
{
    public static string RequireSchema(string schema)
        => PostgreSqlIdentifier.IsUnquoted(schema)
            ? schema
            : throw new ArgumentException("The schema must be an unquoted PostgreSQL identifier.", nameof(schema));
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PostgreSql`. C# namespace: `SharedKernel.PostgreSql`. Assembly/project identity: `SharedKernel.PostgreSql`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
