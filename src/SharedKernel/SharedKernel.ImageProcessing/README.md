# PandaBear.SharedKernel.ImageProcessing

Image decoding and responsive variant generation for shared-kernel workflows.

## Usage

Build an `ImageProcessingRequest` containing input and requested variants, then call `MagickImageProcessor.Process(request, cancellationToken)`. `ImageProcessingLimits` bounds decoded dimensions and pixel count; results contain source dimensions and encoded variants. Supported output enum values include AVIF, WebP, JPEG, and ICO. Keep limits enabled and let consumers own upload validation, storage, and image access policy.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

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

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
