# PandaBear.SharedKernel.AuditTrail.SourceGenerator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Source generator for typed domain-event to audit-trail mappings.

## Usage

Reference this package as a compiler analyzer together with the audit-trail and domain contracts. Declare typed mappings using `PandaBear.AuditTrail.AuditTrailMappingAttribute`. The incremental generator emits mechanical domain-event audit mappings; the consuming application owns which events are audited and what information is safe to record.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.AuditTrail.SourceGenerator --version <validated-candidate-version> --source <private-candidate-feed>
```

Compiler assets are delivered as analyzers. Add the matching runtime contracts explicitly; the runtime package does not bundle this generator. Keep the generator as a development-only reference (`PrivateAssets="all"`) and use a compiler compatible with the documented Roslyn 5.6 baseline.

```csharp
using PandaBear.AuditTrail;
using PandaBear.Domain;

public sealed record ItemCreated : IDomainEvent;
public sealed record ItemAudit(DateTime OccurredAtUtc) : IAuditTrailEntry;

public static class ItemMappings
{
    [AuditTrailMapping]
    public static ItemAudit Map(ItemCreated item, DateTimeOffset recordedAt)
        => new(recordedAt.UtcDateTime);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AuditTrail.SourceGenerator`. C# namespace: `PandaBear.AuditTrail.SourceGenerator`. Assembly/project identity: `SharedKernel.AuditTrail.SourceGenerator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
