# PandaBear.SharedKernel.Idempotency.EntityFrameworkCore

Entity Framework Core persistence provider for reusable idempotency stores.

## Usage

Register `AddIdempotencyStore<TContext>()` with the owning DbContext and optionally configure `IdempotencyStorageOptions`. The package persists reusable idempotency state through EF Core. The application owns provider selection, schema/table policy, migrations, request scope, and the surrounding business operation.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Idempotency.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Idempotency.EntityFrameworkCore;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddIdempotencyStore<AppDbContext>();
}

public sealed class AppDbContext : DbContext;
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Idempotency.EntityFrameworkCore`. C# namespace: `PandaBear.Idempotency.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Idempotency.EntityFrameworkCore`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
