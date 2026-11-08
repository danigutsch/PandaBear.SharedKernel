# SharedKernel.EntityFrameworkCore.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Analyzers for SharedKernel Entity Framework Core model configuration conventions.

## Purpose

`SharedKernel.EntityFrameworkCore` tags a unique index with an application rule through `HasUniqueRule(...)`, so
`SaveChangesTranslatingUniqueViolations` can report a lost uniqueness race as `UniqueConstraintViolationException(rule)` and a handler
can answer with a field error. A unique index without a rule still enforces uniqueness in the database, but a violation escapes as a
raw provider exception and the client gets a server error.

Reference this package only in projects that use `SharedKernel.EntityFrameworkCore`. The analyzer reports nothing when the
compilation does not reference `PandaBear.EntityFrameworkCore.UniqueRuleIndexBuilderExtensions`.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKEF001` | Warning | A unique index is declared without `HasUniqueRule(...)`. |

## Configuration

```ini
dotnet_diagnostic.SKEF001.severity = warning
```

## SKEF001

Reports a call of `IsUnique()` or `IsUnique(true)` on an `IndexBuilder` or `IndexBuilder<T>` (in `OnModelCreating` and in
`IEntityTypeConfiguration<T>.Configure` alike) when the same fluent chain does not call `HasUniqueRule(...)`, before or after
`IsUnique`. The diagnostic points at the `IsUnique` call and names the indexed properties when `HasIndex` is in the chain.

```csharp
builder.HasIndex(x => x.Email).IsUnique();                              // SKEF001
builder.HasIndex(x => x.Email).IsUnique().HasUniqueRule("customer.email");   // fine
builder.HasIndex(x => x.Email).HasUniqueRule("customer.email");              // fine: HasUniqueRule makes the index unique
```

The `[Index(..., IsUnique = true)]` attribute is reported too, because an attribute cannot carry a rule. Configure that index with the
fluent API instead.

```csharp
[Index(nameof(Email), IsUnique = true)]   // SKEF001
public class Customer { ... }
```

Not reported: `IsUnique(false)` and non-constant arguments, non-unique indexes, primary keys and `HasAlternateKey`, and compilations
without `SharedKernel.EntityFrameworkCore`.

Limitation: when the chain result is stored in a local variable, the analyzer accepts a `HasUniqueRule` call on that local
anywhere in the block that declares it. Other indirect forms (an index builder passed to a helper method, a field, or a
`HasUniqueRule` call in another method) are reported; suppress the diagnostic for those. There is no code fix, because the
rule name is chosen by the application.

## Package boundary

This package owns Entity Framework Core model configuration diagnostics. Keep repository-wide style rules in
`SharedKernel.Style.Analyzers` and domain encapsulation rules in `SharedKernel.Domain.Analyzers`.

## See Also

- [SharedKernel.EntityFrameworkCore](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.EntityFrameworkCore/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EntityFrameworkCore.Analyzers`. C# namespace: `PandaBear.EntityFrameworkCore.Analyzers`. Assembly/project identity: `SharedKernel.EntityFrameworkCore.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
