# PandaBear.SharedKernel.DocumentRendering

Structured document models and deterministic HTML rendering primitives.

## Usage

Create a `DocumentRenderRequest` with `DocumentSection`, `DocumentField`, a `DocumentBrandingSnapshot`, and the required privacy classification. Pass it to `HtmlDocumentRenderer.Render` through `IDocumentRenderer` to produce HTML bytes. This package supplies structured rendering; storage, HTTP delivery, access control, and document business rules remain consumer-owned.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.DocumentRendering --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using PandaBear.DocumentRendering;

public static class Example
{
    public static byte[] Render()
    {
        var request = new DocumentRenderRequest("en", "Receipt",
            [new DocumentSection("Items",
                [new DocumentField("Product", "Guidebook", DocumentPrivacyClassification.Public)])],
            Branding: null);
        return new HtmlDocumentRenderer().Render(request);
    }
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.DocumentRendering`. C# namespace: `PandaBear.DocumentRendering`. Assembly/project identity: `SharedKernel.DocumentRendering`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
