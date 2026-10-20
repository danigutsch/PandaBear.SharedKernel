# PandaBear.SharedKernel.Domain.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Entity Framework Core SaveChanges integration for dispatching aggregate domain events.

## Usage

Register `AddDomainEventDispatch<TContext>()` for the application DbContext. The SaveChanges interceptor dispatches aggregate domain events through the configured domain dispatcher. Configure the concrete context, provider, domain handlers, and transaction boundary in the consuming host.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Domain.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Domain.EntityFrameworkCore;

public static class Example
{
    public static IServiceCollection Register(IServiceCollection services)
        => services.AddDomainEventDispatch<AppDbContext>();
}

public sealed class AppDbContext : DbContext;
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain.EntityFrameworkCore`. C# namespace: `PandaBear.Domain.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Domain.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
