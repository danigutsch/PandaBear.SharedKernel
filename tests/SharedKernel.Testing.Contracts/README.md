# PandaBear.SharedKernel.Testing.Contracts

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable contract test marker and trait contracts.

## Usage

Use `ContractHttpClientTestHelper` and `ContractArtifactAssertions` in contract tests, with `TrackingHttpContent` and `ThrowingDisposeStream` for disposal/error-path checks. This package contains test support rather than application runtime composition. Consumers own their HTTP contract artifacts and expected behavior.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Testing.Contracts --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using PandaBear.Testing.Contracts;

public static class Example
{
    public static async Task<string> ReadFixture(CancellationToken ct)
    {
        using var client = ContractHttpClientTestHelper.CreateClient(
            _ => ContractHttpClientTestHelper.JsonResponse("{\"name\":\"Guidebook\"}"));
        using var response = await client.GetAsync("items/1", ct);
        return await response.Content.ReadAsStringAsync(ct);
    }
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Contracts`. C# namespace: `PandaBear.Testing.Contracts`. Assembly/project identity: `SharedKernel.Testing.Contracts`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
