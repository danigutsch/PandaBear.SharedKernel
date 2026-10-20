# PandaBear.SharedKernel.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Entity Framework Core composition, registration, and transaction primitives for shared-kernel workflows.

## Usage

Implement `IDbContextConfiguration<TContext>` to compose model configuration and use the package registration extensions in the owning host. `EfCoreCommandTransactionScope` supplies reusable command transaction mechanics. Consumers retain DbContext ownership, database provider configuration, schemas, migrations, and application transaction policy. Development diagnostics must remain scoped to development use.

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
