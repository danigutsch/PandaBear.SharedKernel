# PandaBear.SharedKernel.InputNormalization

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable input normalization helpers for shared-kernel workflows.

## Usage

Use `StringSanitizer` for trimming/normalization and key generation, `NumericSanitizer.SanitizePrice` for reusable numeric normalization, `ObjectStorageKeyValidator.IsValidRelativeKey` for relative storage keys, and `WebAssetUriSanitizer` for root-relative or HTTPS assets (surrounding white space is trimmed; control characters, inner white space, backslashes, lone surrogates, protocol-relative addresses and user information are refused). Normalization does not replace domain validation, authorization, or storage access checks.

`Sanitize`, `SanitizeNotes` and `NormalizeKey` throw `ArgumentException` when the text is not well-formed UTF-16, such as a lone
surrogate from a truncated or hostile form field, because Unicode normalization refuses it. For untrusted input use `TrySanitize`,
`TrySanitizeNotes` and `TryNormalizeKey`: they return `false` for such text, and otherwise exactly what the throwing methods return, so
the caller can answer a validation error instead of a server error.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

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

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
