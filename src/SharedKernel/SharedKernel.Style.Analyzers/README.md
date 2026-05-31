# SharedKernel.Style.Analyzers

Analyzer project for repository-wide SharedKernel style conventions.

## Purpose

This Roslyn component reports diagnostics for repository rules that are intentionally stricter than
the default .NET analyzer set.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKSTYLE001` | Warning | Method names should not end with `Async` unless an override or interface implementation contract requires it. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKSTYLE001.severity = suggestion

sharedkernel_style_allow_async_suffix_overrides = true
sharedkernel_style_allow_async_suffix_interface_implementations = true
```

## Intentional diagnostic sample

```csharp
public sealed class TourLoader
{
    public async Task<string> LoadAsync(CancellationToken ct)
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
