# PandaBear.SharedKernel.ImageProcessing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Image decoding and responsive variant generation for shared-kernel workflows.

## Usage

Build an `ImageProcessingRequest` containing input and requested variants, then call `MagickImageProcessor.Process(request, cancellationToken)`. `ImageProcessingLimits` bounds decoded dimensions and pixel count; results contain source dimensions and encoded variants. Supported output enum values include AVIF, WebP, JPEG, and ICO. Keep limits enabled and let consumers own upload validation, storage, and image access policy.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.ImageProcessing --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using PandaBear.ImageProcessing;

public static class Example
{
    public static ImageProcessingResult Thumbnail(Stream image, CancellationToken ct)
        => MagickImageProcessor.Process(new ImageProcessingRequest(image,
            [new ImageVariantRequest("thumbnail", ImageOutputFormat.WebP, 320, 80)],
            ImageProcessingLimits.WebDefault), ct);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ImageProcessing`. C# namespace: `PandaBear.ImageProcessing`. Assembly/project identity: `SharedKernel.ImageProcessing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
