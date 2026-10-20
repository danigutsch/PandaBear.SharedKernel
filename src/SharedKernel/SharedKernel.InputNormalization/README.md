# PandaBear.SharedKernel.InputNormalization

Reusable input normalization helpers for shared-kernel workflows.

## Usage

Use `StringSanitizer` for trimming/normalization and key generation, `NumericSanitizer.SanitizePrice` for reusable numeric normalization, `ObjectStorageKeyValidator.IsValidRelativeKey` for relative storage keys, and `WebAssetUriSanitizer` for root-relative or HTTPS assets. Normalization does not replace domain validation, authorization, or storage access checks.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. No post-namespace candidate is advertised as available yet. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.InputNormalization --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using PandaBear.InputNormalization;

public static class Example
{
    public static string NormalizeName(string name)
        => StringSanitizer.NormalizeKey(name);

    public static bool IsSafeKey(string key)
        => ObjectStorageKeyValidator.IsValidRelativeKey(key, maxLength: 200);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.InputNormalization`. C# namespace: `PandaBear.InputNormalization`. Assembly/project identity: `SharedKernel.InputNormalization`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
