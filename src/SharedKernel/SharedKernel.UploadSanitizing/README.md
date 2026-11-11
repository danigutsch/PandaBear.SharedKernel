# PandaBear.SharedKernel.UploadSanitizing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Dependency-free upload sanitizing for JPEG, PNG and WebP, and opt-in AVIF: format recognition by content, metadata removal without
re-encoding, animation detection, and the image size from its header. No native library is needed, unlike `PandaBear.SharedKernel.ImageProcessing` (ImageMagick).

## Usage

`ImageSanitizer` accepts an upload only when its own bytes are a well-formed JPEG, PNG or WebP. The file name and the declared content type
play no part. It removes the metadata that can reveal where and with what a photo was taken, and keeps everything else byte for byte:

| Format | Removed | Kept |
| --- | --- | --- |
| JPEG | APP1 (Exif with GPS, XMP), APP13 (Photoshop, IPTC), COM, fill bytes, bytes after the end marker | JFIF, the ICC profile (APP2), every other segment, the scans |
| PNG | `eXIf`, `tEXt`, `iTXt`, `zTXt`, `tIME`, bytes after `IEND` | every other chunk with its CRC, including `iCCP` |
| WebP | `EXIF`, `XMP ` and their `VP8X` flags; the RIFF size is rewritten | every other chunk, including `ICCP`, and the other flags |
| AVIF (opt-in) | nothing | everything: the file is returned unchanged |

The result is a `Result<SanitizedImage>` from `PandaBear.SharedKernel.Results`: the format, the cleaned bytes, the width and height, the
usual file extension and the media type, whether the metadata was removed (`MetadataRemoved`) and whether the image is animated
(`IsAnimated`). Untrusted input never throws. Every rejection is an invalid result with one message on `ImageSanitizerOptions.Field`:

- `NotAnImageMessage`: the leading bytes are not a JPEG, PNG or WebP signature, nor an AVIF one when AVIF is allowed.
- `MalformedMessage`: the structure does not parse exactly, for example a length beyond the file, a missing end marker or image data, a
  second frame header or `IHDR`, a zero width, a WebP whose RIFF size does not match, or an AVIF box that does not fit its parent.
- `TooLargeMessage`: more bytes than `ImageSanitizerOptions.MaxBytes` (default 10 MiB).
- `DimensionsTooLargeMessage`: a width or height above `ImageSanitizerOptions.MaxDimension` (default 20,000).
- `AnimatedMessage`: an animated image when `ImageSanitizerOptions.RejectAnimation` is set.

### Animation

`IsAnimated` is true for an APNG (an `acTL` chunk), an animated WebP (an `ANIM` or `ANMF` chunk, or the animation flag of `VP8X`) and an
AVIF image sequence (the `avis` brand or a `moov` box). Animated images are accepted by default; set `RejectAnimation = true` to refuse them
with `AnimatedMessage`, for example for a logo or an avatar.

### AVIF

AVIF is refused unless `AllowAvif = true`. An allowed AVIF is recognized by the `avif` or `avis` brand of its leading `ftyp` box, every
ISOBMFF box must fit its parent exactly, and the size is the `ispe` property associated in `ipma` with the primary item named by `pitm`.
Every `ispe` in the file, including those of an alpha plane or a thumbnail, must be within `MaxDimension`.

The metadata of an AVIF is not removed, and `MetadataRemoved` is false. Exif and XMP are items whose byte ranges are listed in the `iloc`
box; removing them means rewriting those offsets, and every other item's, which this package does not do rather than risk returning a
damaged file. So AVIF is opt-in: allow it only where keeping the camera and location data is acceptable, or re-encode the image afterwards,
for example with `PandaBear.SharedKernel.ImageProcessing`.

Compare the message constants to map a rejection to your own wording. Declared lengths inside the file are checked against the bytes present
before they are used, so a forged length never sizes an allocation; the work is linear in the input size. Pixel data is not decoded, so a
decoder that runs later still needs its own limits.

Three entry points share the same checks:

- `Sanitize(ReadOnlySpan<byte>, options)` leaves the input unchanged and returns the image in one new array.
- `SanitizeInPlace(Memory<byte>, options)` allocates no copy: the kept bytes move towards the start of the buffer and the result is a slice of
  it. The buffer's contents are unspecified after a rejection.
- `Sanitize(Stream, options, ct)` reads from the current position, at most `MaxBytes` plus one byte, and never disposes the stream. A seekable
  stream's length only sizes the first buffer, up to 1 MiB.

The package uses no reflection and is NativeAOT and trimming compatible. Malware scanning, storage, naming and who may upload stay with the
application.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.UploadSanitizing --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using System.Security.Cryptography;
using PandaBear.UploadSanitizing.Images;
using PandaBear.Results;

public static class Example
{
    public static async Task<Result<string>> Store(Stream upload, string folder, CancellationToken ct)
    {
        var sanitized = await ImageSanitizer.Sanitize(upload, ImageSanitizerOptions.Default with { Field = "photo" }, ct);
        if (sanitized.IsFailure)
        {
            return sanitized.ConvertError<SanitizedImage, string>();
        }

        var image = sanitized.Value;
        var name = Convert.ToHexStringLower(SHA256.HashData(image.Bytes.Span)) + image.FileExtension;
        await File.WriteAllBytesAsync(Path.Combine(folder, name), image.Bytes, ct);
        return Result.Ok(name);
    }
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.UploadSanitizing`. C# namespaces: `PandaBear.UploadSanitizing.Images`. Assembly/project identity: `SharedKernel.UploadSanitizing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
