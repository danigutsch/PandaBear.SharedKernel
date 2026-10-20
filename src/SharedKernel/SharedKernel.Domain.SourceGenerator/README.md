# PandaBear.SharedKernel.Domain.SourceGenerator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Source generator for SharedKernel.Domain model support.

## Usage

Reference this package as a compiler analyzer together with `PandaBear.SharedKernel.Domain`. Annotate partial model types with `GenerateValueObjectAttribute` or `GenerateModelSupportAttribute`; assembly defaults use `GenerateModelSupportDefaultsAttribute`. The generator emits mechanical model support during compilation. Optional JSON and EF Core generation requires the corresponding framework references in the consumer.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Domain.SourceGenerator --version <validated-candidate-version> --source <private-candidate-feed>
```

Compiler assets are delivered as analyzers. Add the matching runtime contracts explicitly; the runtime package does not bundle this generator. Keep the generator as a development-only reference (`PrivateAssets="all"`) and use a compiler compatible with the documented Roslyn 5.6 baseline.

```csharp
using PandaBear.Domain;

[GenerateValueObject(UnderlyingType = typeof(Guid))]
public readonly partial record struct ProductId;
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain.SourceGenerator`. C# namespace: `PandaBear.Domain.SourceGenerator`. Assembly/project identity: `SharedKernel.Domain.SourceGenerator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
