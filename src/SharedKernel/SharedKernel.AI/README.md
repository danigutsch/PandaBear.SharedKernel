# PandaBear.SharedKernel.AI

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable AI-assisted image text generation contracts and LiteLLM client.

## Usage

Construct `LiteLlmImageTextGenerator` with an application-configured `HttpClient` and `LiteLlmImageTextGeneratorOptions`, then call `IImageTextGenerator.GenerateImageText` with an image request and cancellation token. The result contains alt text and an optional caption. The host owns endpoint/API credentials, model selection, consent, sensitive-image policy, and telemetry; do not commit credentials or image captures.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.AI --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using PandaBear.AI;

public static class Example
{
    public static ValueTask<ImageTextGenerationResult> Generate(
        IImageTextGenerator generator, Stream image, CancellationToken ct)
        => generator.GenerateImageText(new ImageTextGenerationRequest
        {
            Image = image,
            ContentType = "image/jpeg",
            Language = "en"
        }, ct);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AI`. C# namespace: `PandaBear.AI`. Assembly/project identity: `SharedKernel.AI`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
