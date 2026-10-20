# PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Entity Framework Core outbox and inbox persistence provider for typed integration-event messaging.

## Usage

Compose the outbox/inbox registration extensions with the owning EF Core context and configure `IntegrationEventStorageOptions` and relay options. The package includes durable outbox storage, claiming, relay, and PostgreSQL transport components. Producers and consumers register separately. Applications retain schemas, migrations, transport composition, handler business rules, and retention policy.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

public static class Example
{
    public static IServiceCollection RegisterProducer(IServiceCollection services)
        => services.AddIntegrationEventOutbox<AppDbContext>();
}

public sealed class AppDbContext : DbContext;
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore`. C# namespace: `PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
