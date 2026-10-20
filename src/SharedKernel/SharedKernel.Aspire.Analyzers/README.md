# SharedKernel.Aspire.Analyzers

Analyzer project for SharedKernel Aspire conventions.

## Purpose

This Roslyn component reports diagnostics for Aspire-specific repository rules. It keeps optional
Aspire conventions out of broad repository-wide style analyzer packages.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKASPIRE001` | Warning | Aspire container image pins should pair `WithImageTag(...)` with `WithImageSHA256(...)` and use bare verified digests. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKASPIRE001.severity = warning
```

## Package boundary

This package owns Aspire-specific diagnostics. Keep repository-wide style rules in
`SharedKernel.Style.Analyzers`, test-only rules in `SharedKernel.Testing.Analyzers`, and mediator
rules in `SharedKernel.Mediator.Analyzers`.

## See Also

- [SharedKernel.Aspire.CodeFixes](../SharedKernel.Aspire.CodeFixes/README.md)
- [Aspire official documentation](https://aspire.dev)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Analyzers`. C# namespace: `PandaBear.Aspire.Analyzers`. Assembly/project identity: `SharedKernel.Aspire.Analyzers`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
