# PandaBear.SharedKernel.Results.SourceGenerator

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Source generator for centralized SharedKernel.Results error catalogs.

## Usage

Reference this package as a compiler analyzer together with `PandaBear.SharedKernel.Results`. Define centralized static result-returning error factories/properties; the incremental generator discovers supported declarations and emits a typed error catalog provider. Keep stable identifiers and business error ownership in the consumer. The generator supplies catalog binding rather than business validation rules.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Results.SourceGenerator --version <validated-candidate-version> --source <private-candidate-feed>
```

`PandaBear.SharedKernel.Results` already bundles this generator. Ordinary consumers install that runtime package instead; do not install both generator assets. This separate package serves explicit compiler-tooling composition. The example also requires the Results contracts.

```csharp
using PandaBear.Results;

public static class ItemErrors
{
    public static Result Missing() => Result.NotFound("Item was not found.");
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Results.SourceGenerator`. C# namespace: `PandaBear.Results.SourceGenerator`. Assembly/project identity: `SharedKernel.Results.SourceGenerator`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
