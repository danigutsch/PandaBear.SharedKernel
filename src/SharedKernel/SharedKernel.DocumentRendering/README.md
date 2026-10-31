# PandaBear.SharedKernel.DocumentRendering

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Structured document models and deterministic HTML rendering primitives.

## Usage

Create a `DocumentRenderRequest` with `DocumentSection`, `DocumentField`, a `DocumentBrandingSnapshot`, and the required privacy classification. Pass it to `HtmlDocumentRenderer.Render` through `IDocumentRenderer` to produce HTML bytes. This package supplies structured rendering; storage, HTTP delivery, access control, and document business rules remain consumer-owned.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

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

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Logos

`DocumentBrandingSnapshot.LogoUri` renders when it is a root-relative or HTTPS URI, or a base64 `data:` URI of a PNG,
JPEG or WebP image whose bytes match the declared type (up to 60,000 characters). SVG and other types are never
rendered. An embedded logo makes the finalized document self-contained, so it does not change when the hosted file does.
The renderer checks the type signature only; the application that accepts the upload must decode, scan and re-encode it.
