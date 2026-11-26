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

Compiler assets are delivered as analyzers. Reference the matching runtime contracts and keep the generator as a development-only reference (`PrivateAssets="all"`). Use a compiler compatible with the Roslyn version pinned in [Directory.Packages.props](../../../Directory.Packages.props).

```csharp
using SharedKernel.Domain;

[GenerateValueObject(UnderlyingType = typeof(Guid))]
public readonly partial record struct ProductId;
```

## Choosing generated model support

- Opt a top-level, nongeneric partial entity or aggregate class into identity equality when it has a stable `IIdentified<TId>.Id` and uses the same equality rule for its full lifetime. Distinct instances with an unassigned identifier compare unequal. Assign identity before storing the model in a hash-based collection. `SKMDL022` identifies an authored `Equals(object)` or `GetHashCode()` that overlaps generated equality. Set `Identity = false` to keep the authored behavior.
- Use a generated `readonly partial record struct` for a single supported scalar with value equality. The generator provides construction and optional parsing. Use `ValidateValue` for an additional caller-owned invariant and check the behavior of the CLR default value at input boundaries. Accessing `Value` on a default string-backed wrapper throws `InvalidOperationException`.
- Use the explicit `ValueObject` base or an authored equality implementation for multi-field models. Specify how collections compare, including ordering and element equality. Keep aggregate transitions and their validation in the aggregate.
- Opt into generated JSON or EF Core converters only where the consumer uses the corresponding adapter. Verify the serialized scalar representation, validated construction, and materialization against the existing consumer contract before adoption.

ViajantesTurismo already uses generated identity equality for its Customer, Booking, Tour, and EditablePublicContent models. Booking, Tour and Customer assign the application command's identifier at creation, including transaction retries. Multi-field models such as Address and ContactInfo use explicit equality components and keep their application validation and persisted shape.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Domain.SourceGenerator`. C# namespace: `SharedKernel.Domain.SourceGenerator`. Assembly/project identity: `SharedKernel.Domain.SourceGenerator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
