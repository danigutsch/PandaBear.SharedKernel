# PandaBear.SharedKernel.Branding

Reusable branding settings contracts, validation, and HTTP client primitives.

## Usage

Use `BrandingSettings` and contract constants for reusable branding validation, `IBrandingSettingsStore` for persistence abstraction, and the public/management branding clients with an explicitly configured `HttpClient`. DTOs and routes describe the reusable contract; the host owns authorization, endpoint base addresses, persistence, cache policy, and application defaults.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Branding --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using PandaBear.Branding;

public static class Example
{
    public static async Task<BrandingSettingsDto> Read(
        HttpClient configuredClient, CancellationToken ct)
    {
        var client = new BrandingApiClient(configuredClient);
        return await client.GetPublicSettings(ct);
    }
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Branding`. C# namespace: `PandaBear.Branding`. Assembly/project identity: `SharedKernel.Branding`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
