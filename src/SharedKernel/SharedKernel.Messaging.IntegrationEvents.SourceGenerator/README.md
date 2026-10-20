# PandaBear.SharedKernel.Messaging.IntegrationEvents.SourceGenerator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Source generator for typed domain-event to integration-event messaging mappings.

## Usage

Reference this package as a compiler analyzer with the domain and integration-event contracts. Declare `IntegrationEventMappingAttribute` mappings and explicit consumer metadata/handlers. The generator emits typed domain-to-integration mappings and closed dispatch paths; it reports missing or duplicate consumer registrations at compilation. Applications own payload contracts, business handlers, JSON metadata, and host scopes.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Messaging.IntegrationEvents.SourceGenerator --version <validated-candidate-version> --source <private-candidate-feed>
```

Compiler assets are delivered as analyzers. Add the matching runtime contracts explicitly; the runtime package does not bundle this generator. Keep the generator as a development-only reference (`PrivateAssets="all"`) and use a compiler compatible with the documented Roslyn 5.6 baseline.

```csharp
using PandaBear.Domain;
using PandaBear.Messaging.IntegrationEvents;

public sealed record ItemCreated(Guid Id, DateTimeOffset Time) : IDomainEvent;
public sealed record ItemPublished(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventType => "example.item.published";
    public static int EventVersion => 1;
}

public static class ItemMappings
{
    [IntegrationEventMapping]
    public static ItemPublished Map(ItemCreated item, Guid eventId, DateTimeOffset occurredAt)
        => new(eventId, occurredAt);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents.SourceGenerator`. C# namespace: `PandaBear.Messaging.IntegrationEvents.SourceGenerator`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents.SourceGenerator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
