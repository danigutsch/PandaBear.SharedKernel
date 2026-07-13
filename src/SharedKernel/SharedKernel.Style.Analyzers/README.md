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
| `SKSTYLE005` | Warning | Generic type names should not include suffixes that repeat generic arity. |
| `SKSTYLE006` | Warning | Catch filters should not suppress every `OperationCanceledException` without checking the operation token. |
| `SKSTYLE007` | Warning | Production logging should use source-generated `LoggerMessage` methods instead of direct `ILogger.Log*` calls. |
| `SKSTYLE008` | Warning | Domain event types implementing `IDomainEvent` should end with `DomainEvent`. |
| `SKSTYLE009` | Warning | Methods returning `SharedKernel.Results.Result` must be able to return a failure `Result`; use a non-`Result` return type when every reachable return is successful. Overrides and interface implementations are excluded. |
| `SKSTYLE010` | Warning | Methods returning `SharedKernel.Results.Result<T>` only as `Ok` or `NotFound` should return `Option<T>`. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKSTYLE001.severity = warning
dotnet_diagnostic.SKSTYLE002.severity = warning
dotnet_diagnostic.SKSTYLE003.severity = warning
dotnet_diagnostic.SKSTYLE004.severity = warning
dotnet_diagnostic.SKSTYLE005.severity = warning
dotnet_diagnostic.SKSTYLE006.severity = warning
dotnet_diagnostic.SKSTYLE007.severity = warning
dotnet_diagnostic.SKSTYLE008.severity = warning
dotnet_diagnostic.SKSTYLE009.severity = warning
dotnet_diagnostic.SKSTYLE010.severity = warning
sharedkernel_style_allow_async_suffix_overrides = true
sharedkernel_style_allow_async_suffix_interface_implementations = true
```

Analyzer descriptors default to `warning`. Repository `.editorconfig` can stage cleanup-heavy
adoption while the existing baseline is being retired.

`SKSTYLE004` follows the same staged rollout. The first pass excludes generated files,
files containing only partial top-level types, and a short list of production files that still carry
intentional grouped top-level types. Test files are included so extracted helpers move to their own
named files instead of becoming file-local helper types beside a test class.

`SKSTYLE008` is scoped to source types that directly or indirectly implement
`SharedKernel.Domain.IDomainEvent`. DTOs, read models, notification wrappers, and generic types that
only constrain `TDomainEvent` are not reported.

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

## Package boundary

This package owns repository-wide style diagnostics. Keep optional technology rules such as Aspire,
EF Core, Dapper, Azure SDKs, browser tooling, and test-only rules in narrower capability packages.

## See Also

- [SharedKernel.Style.CodeFixes](../SharedKernel.Style.CodeFixes/README.md)
- [SharedKernel.Aspire.Analyzers](../SharedKernel.Aspire.Analyzers/README.md)
- `docs/CODING_GUIDELINES.md`
