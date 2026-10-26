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
using PandaBear.EntityFrameworkCore;

public sealed class AppDbContext : DbContext;
public sealed class AppConfiguration : IDbContextConfiguration<AppDbContext>;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddDbContextConfiguration<AppDbContext>(new AppConfiguration());
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EntityFrameworkCore`. C# namespace: `PandaBear.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
