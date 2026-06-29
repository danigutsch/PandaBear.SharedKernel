# SharedKernel.Style.Analyzers

Analyzer project for repository-wide SharedKernel style conventions.

## Purpose

This Roslyn component reports diagnostics for repository rules that are intentionally stricter than
the default .NET analyzer set.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKSTYLE001` | Warning | Method names should not end with `Async` unless an override or interface implementation contract requires it. |
| `SKSTYLE002` | Warning | `CancellationToken` parameters should use the canonical name `ct`. |
| `SKSTYLE003` | Warning | `CancellationToken` parameters should not declare default values. |
| `SKSTYLE004` | Warning | Source files should not declare more than one top-level type unless they fall under a documented rollout exception. |
| `SKSTYLE005` | Warning | Aspire container image pins should pair `WithImageTag(...)` with `WithImageSHA256(...)` and use bare verified digests. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKSTYLE001.severity = suggestion
dotnet_diagnostic.SKSTYLE002.severity = suggestion
dotnet_diagnostic.SKSTYLE003.severity = suggestion
dotnet_diagnostic.SKSTYLE004.severity = suggestion
dotnet_diagnostic.SKSTYLE005.severity = suggestion
sharedkernel_style_allow_async_suffix_overrides = true
sharedkernel_style_allow_async_suffix_interface_implementations = true
```

The repository currently stages `SKSTYLE002` and `SKSTYLE003` as suggestions for rollout.
They can be raised to warning or error after the existing codebase is cleaned up.

`SKSTYLE004` follows the same staged rollout. The first pass excludes generated files,
files containing only partial top-level types, and a short list of production files that still carry
intentional grouped top-level types. Test files are included so extracted helpers move to their own
named files instead of becoming file-local helper types beside a test class.

`SKSTYLE005` has a code fix that inserts uncompilable placeholders. This keeps the fix discoverable
without creating a false supply-chain control. Replace the placeholders with verified registry values,
and pass the bare 64-character value to `WithImageSHA256(...)`.

## Intentional diagnostic sample

```csharp
public sealed class TourLoader
{
    public async Task<string> LoadAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return "VT-42";
    }
}
```

## Exceptions intentionally allowed by default

- overrides that must keep an inherited method name such as `ExecuteAsync`
- interface implementations that must keep an existing contract name such as `DisposeAsync`

## See Also

- [SharedKernel.Style.CodeFixes](../SharedKernel.Style.CodeFixes/README.md)
- `docs/CODING_GUIDELINES.md`
