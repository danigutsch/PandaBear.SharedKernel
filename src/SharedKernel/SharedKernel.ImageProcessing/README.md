# PandaBear.SharedKernel.ImageProcessing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Image decoding and responsive variant generation for shared-kernel workflows.

## Usage

Build an `ImageProcessingRequest` containing input and requested variants, then call `MagickImageProcessor.Process(request, cancellationToken)`. `ImageProcessingLimits` bounds decoded dimensions and pixel count; results contain source dimensions and encoded variants. Supported output enum values include AVIF, WebP, JPEG, ICO, and lossless PNG; PNG output ignores the quality value and carries no text, time, or EXIF chunks. Keep limits enabled and let consumers own upload validation, storage, and image access policy.

Call `MagickImageProcessor.Probe(stream, limits)` before processing to read the decoded `ImageInputFormat` (JPEG, PNG, WebP, or AVIF), the stored dimensions, and whether the image is animated, without decoding pixel data. Use it to compare the decoded format with a declared content type or to refuse animated uploads. The probe applies the same stream, signature, and limit checks as `Process` and throws `ImageProcessingException` when a PNG or WebP container is malformed.

`ImageAnimationDetector.Detect` walks PNG and WebP container headers only, so it can run before any decode, from a span or a seekable stream. It reports an `ImageAnimationStatus`: a PNG is animated when `acTL` precedes the first `IDAT`, and a WebP when its `VP8X` animation flag is set or it holds `ANIM` or `ANMF` chunks. Offsets use 64-bit arithmetic and are checked against the remaining length before each read, the walk stops after `ImageAnimationDetector.MaxChunkCount` chunks, and truncated or inconsistent containers report `Malformed`, which is also the enum's default value. Treat every status other than `Static` as a rejection when animation is not allowed.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.ImageProcessing --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using SharedKernel.ImageProcessing;

public static class Example
{
    public static ImageProcessingResult Thumbnail(Stream image, CancellationToken ct)
        => MagickImageProcessor.Process(new ImageProcessingRequest(image,
            [new ImageVariantRequest("thumbnail", ImageOutputFormat.WebP, 320, 80)],
            ImageProcessingLimits.WebDefault), ct);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ImageProcessing`. C# namespace: `SharedKernel.ImageProcessing`. Assembly/project identity: `SharedKernel.ImageProcessing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
