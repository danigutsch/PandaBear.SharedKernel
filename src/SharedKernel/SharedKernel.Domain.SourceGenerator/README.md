# PandaBear.SharedKernel.Domain.SourceGenerator

Source generator for SharedKernel.Domain model support.

## Usage

Reference this package as a compiler analyzer together with `PandaBear.SharedKernel.Domain`. Annotate partial model types with `GenerateValueObjectAttribute` or `GenerateModelSupportAttribute`; assembly defaults use `GenerateModelSupportDefaultsAttribute`. The generator emits mechanical model support during compilation. Optional JSON and EF Core generation requires the corresponding framework references in the consumer.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

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

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
