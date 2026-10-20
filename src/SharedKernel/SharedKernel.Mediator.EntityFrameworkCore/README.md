# PandaBear.SharedKernel.Mediator.EntityFrameworkCore

Mediator pipeline behaviors and dispatch integrations backed by Entity Framework Core.

## Usage

Register `AddEfCoreCommandTransaction<TContext, TRequest, TResponse>()` for a concrete command pipeline. `EfCoreCommandTransactionBehavior<TRequest, TResponse>` wraps the continuation using the configured DbContext transaction support. Keep request handlers and business rules in the consumer and compose only the command paths that require this transaction boundary.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Mediator.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Mediator;
using PandaBear.Mediator.EntityFrameworkCore;

public sealed record SaveItem : ICommand<int>;
public sealed class AppDbContext : DbContext;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddEfCoreCommandTransaction<AppDbContext, SaveItem, int>();
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Mediator.EntityFrameworkCore`. C# namespace: `PandaBear.Mediator.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Mediator.EntityFrameworkCore`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
