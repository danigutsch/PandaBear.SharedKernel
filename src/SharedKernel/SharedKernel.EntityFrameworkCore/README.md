# PandaBear.SharedKernel.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Entity Framework Core composition, registration, and transaction primitives for shared-kernel workflows.

## Usage

Implement `IDbContextConfiguration<TContext>` to compose model configuration and use the package registration extensions in the owning host. `EfCoreCommandTransactionScope` supplies reusable command transaction mechanics. Consumers retain DbContext ownership, database provider configuration, schemas, migrations, and application transaction policy. `ApplyDbContextOptionConfigurations` enables detailed errors and sensitive data logging for every context when the host passes `enableDevelopmentDiagnostics: true`; pass it only in local development.

## Composing read and write configuration

`IDbContextConfiguration<TContext>` declares three phases: `ConfigureConventions`, `ConfigureOptions`, and `ConfigureModel`. Each has a default interface method that does nothing, so a configuration implements only the phases it uses and needs no empty overrides. Default interface methods are available on every target framework of this package.

`ReadDbContextConfiguration<TContext>` and `WriteDbContextConfiguration<TContext>` are small baselines that set only the default query tracking behavior through `UseQueryTrackingBehavior`: `QueryTrackingBehavior.NoTracking` for the read side and `QueryTrackingBehavior.TrackAll`, the EF Core default, for the write side. EF Core documents no-tracking as the right default for read-only results and `AsTracking` or `AsNoTracking` as per-query overrides ([Tracking vs. No-Tracking Queries](https://learn.microsoft.com/en-us/ef/core/querying/tracking)). The baselines set no provider, schema, model, or diagnostics option; the application keeps those in its own configuration.

A context composes baselines and its own configuration by registering them in order, with no base class. `ApplyDbContextOptionConfigurations` applies them in registration order, so the last registration wins when two configurations set the same option:

```csharp
services.AddDbContextConfiguration(new ReadDbContextConfiguration<AppReadDbContext>());
services.AddDbContextConfiguration(new AppReadConfiguration());
services.AddDbContext<AppReadDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    services.ApplyDbContextOptionConfigurations<AppReadDbContext>(options, enableDevelopmentDiagnostics: false);
});
```

A module that needs a SaveChanges hook registers an `IDbContextConfiguration<TContext>` that adds an interceptor with `AddInterceptors` ([Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)); `AddDomainEventDispatch<TContext>()` in `PandaBear.SharedKernel.Domain.EntityFrameworkCore` works this way.

## Design: explicit composition over generated configuration

Three ways to remove per-context configuration boilerplate were compared:

| Approach | Visible in source | AOT and trimming | Startup surface | Cost |
| --- | --- | --- | --- | --- |
| Explicit `IDbContextConfiguration<TContext>` composition | Yes, one registration line per configuration | Typed calls, no reflection | `AddDbContextConfiguration` and `ApplyDbContextOptionConfigurations` | None beyond the registrations |
| Attributes such as `[ReadDbContextConfiguration]` on a partial context with a source generator | No, the behavior lives in generated code | Compatible when written without reflection scanning | One more attribute package and generator contract | A new `SharedKernel.*SourceGenerator` package with snapshot, consumption, and API baselines |
| Analyzer plus code fix that writes the partial | Yes, once written | Compatible | None at runtime | An analyzer, a code fix, and their tests for a few lines of code |

The decision is explicit composition. The boilerplate a generator or code fix would remove is a context's constructor parameter and the loop that calls `ConfigureConventions` and `ConfigureModel`; this repository has nine such contexts, all in test projects, none in `src` or `samples`, and the baselines reduce the per-context wiring to one registration line per side. That is not enough repeated, real boilerplate to justify a generated or scaffolded file, and generated configuration would hide the option a reader needs to see. Revisit the decision when a consuming repository reports repeated partial contexts that the baselines do not cover.

To migrate a context that implements `IDbContextConfiguration<TContext>` for the baseline behavior, delete the tracking option from that implementation and register the matching baseline before it. Implementations that set other options or model configuration stay unchanged.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.EntityFrameworkCore;

public sealed class AppDbContext : DbContext;
public sealed class AppConfiguration : IDbContextConfiguration<AppDbContext>;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddDbContextConfiguration<AppDbContext>(new AppConfiguration());
}
```

## Unique-rule violations

An existence check before an insert cannot stop a concurrent request from winning the race; the unique index is the real
guard. Report that loss as an application rule instead of a provider exception, without Application code ever seeing EF Core:

1. Name the rule next to the index, in the infrastructure model configuration: `builder.HasIndex(c => c.Email).HasUniqueRule("customer.email")`.
2. Save through `dbContext.SaveChangesTranslatingUniqueViolations(parser, ct)`. A violation of a tagged index throws
   `SharedKernel.BuildingBlocks.UniqueConstraintViolationException` with `Rule` set (the provider exception is the inner exception).
   Violations of untagged indexes and every other failure propagate unchanged.
3. Catch `UniqueConstraintViolationException` in the application handler and return the same validation result as the pre-check.

The parser (`IUniqueViolationParser`) holds all provider knowledge and is registered by the provider package, for example
`AddSqliteUniqueViolationParser()` from `SharedKernel.EntityFrameworkCore.Sqlite` or `AddNpgsqlUniqueViolationParser()` from
`SharedKernel.EntityFrameworkCore.Npgsql`. A violation is matched to an index by
constraint name (the index database name) or by table and every column, ignoring case. Another database needs one parser class.

### Concurrency conflicts

`dbContext.SaveChangesTranslatingConflicts(parser, ct)` behaves like `SaveChangesTranslatingUniqueViolations` and additionally
translates `DbUpdateConcurrencyException` (an update or delete whose row changed or disappeared since it was read, or whose
concurrency token no longer matches) into `SharedKernel.BuildingBlocks.ConcurrencyConflictException`. The provider exception is kept as
the inner exception and the message never contains data values. Catch it in the application handler and report a conflict instead of
an unexpected failure. `SaveChangesTranslatingUniqueViolations` is unchanged and does not translate conflicts.

### Results instead of exceptions

Code that returns a `SharedKernel.Results.Result` and does not run through the mediator's `UniqueViolationBehavior` and
`ConcurrencyConflictBehavior` can save with `dbContext.SaveChangesAsResult(parser, uniqueRuleResult, ct)` and need no try/catch:

- success is `Result.Ok()`;
- a violated tagged index returns `uniqueRuleResult(rule)`, typically the same field error as the pre-check;
- a stale row returns a `Conflict` result with the code `ResultErrorCodes.ConcurrencyConflict` and
  `DbContextResultExtensions.ConcurrencyConflictMessage`, the same result as the mediator's `ConcurrencyConflictResults.Stale()`.

After a reported violation or conflict the change tracker is cleared, so the context can be used again; every other failure propagates
unchanged.

```csharp
public Task<Result> Save(CancellationToken ct) => db.SaveChangesAsResult(parser, rule => rule switch
{
    "catalog.slug" => Result.Invalid("This slug is already used.", "slug", "This slug is already used."),
    _ => Result.Conflict("A value that must be unique is already in use."),
}, ct);
```

## JSON list columns

`property.HasJsonConversion()` maps an `IReadOnlyList<T>` of plain values (strings, numbers, ids, small records) to one required JSON
text column, written with `JsonSerializerOptions.Web` (camel case). Lists are compared by their JSON, so assigning an equal list writes
nothing and a change made in place is detected. Use it for lists that are read and written with their owner and never queried.

```csharp
builder.Property(tour => tour.Cities).HasJsonConversion();
```

Entity Framework Core 10's own JSON mapping is the first choice for a new model: primitive collections for lists of plain values, and
`ComplexCollection(...).ToJson()` for lists of reference-type records. Those keep a different stored shape (the CLR property names unless
renamed with `HasJsonPropertyName`, no value-type records) and a different model, so moving an existing value-converted column to them
needs a migration and a data rewrite; `HasJsonConversion` keeps such columns as they are. It uses reflection-based `System.Text.Json`,
so it is marked as not trimming- or Native-AOT-compatible.

## Content revision

`RevisionCache` (`SharedKernel.Caching`) reads a revision number that grows with every content change. This package keeps it on the
writing side. The application owns the row: an entity with a `long` value, in a table that holds exactly one row (seed it with `HasData`
at 0).

```csharp
public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
    this.SaveChangesIncrementingRevision<ContentRevision>(
        row => row.Value,
        ct => base.SaveChangesAsync(acceptAllChangesOnSuccess, ct),
        cancellationToken);

public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
    throw new NotSupportedException("Use SaveChangesAsync so the content revision grows with the change.");
```

- When any entity other than the revision row was added, modified or deleted (changes are detected first), the save and a single
  `UPDATE ... SET Value = Value + 1` run in one transaction: concurrent writers never lose a bump, and a failed save leaves the revision
  alone. Inside the caller's transaction both commit or roll back with it; otherwise the helper opens and commits its own.
- A save without content changes, or one that only changes the revision row, does not increment it.
- `dbContext.IncrementRevision<TRevision>(row => row.Value, ct)` increments it alone, for a change made without tracked entities (for
  example `ExecuteDelete` in the caller's transaction).
- A change made with SQL outside the context does not increment the revision.

It is a save helper called from the override rather than a `SaveChangesInterceptor`, because an interceptor that opens the transaction
in `SavingChanges` cannot guarantee to close it: a failure after `SavingChanges` but before the save starts (for example in another
interceptor) reaches no `SaveChangesFailed`, and the context would keep the transaction open.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EntityFrameworkCore`. C# namespace: `SharedKernel.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Applying migrations at start-up

`await app.Services.ApplyMigrations<AppDbContext>();` applies every pending migration of the context in a new service scope and leaves an
up-to-date database unchanged. Call it once at start-up, before the application accepts requests. It returns `true` after migrating, and
`false` without doing anything when the context uses a non-relational provider, such as the in-memory provider of a test host, so a host
can skip its other relational schema work the same way. `EnsureMigrationsApplied` passes for such a context.

When several hosts share one database and only one owns the schema, the others call
`await app.Services.EnsureMigrationsApplied<AppDbContext>();` instead. It reads the applied migrations without writing, so it works on a
read-only connection, and throws `InvalidOperationException` naming the context, the number of pending migrations and the first one while
the owner has not migrated yet. A missing database counts as one without any migration and is not created.
