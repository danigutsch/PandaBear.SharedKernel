# SharedKernel.Aspire.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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

- [SharedKernel.Aspire.CodeFixes](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Aspire.CodeFixes/README.md)
- [Aspire official documentation](https://aspire.dev)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.Analyzers`. C# namespace: `PandaBear.Aspire.Analyzers`. Assembly/project identity: `SharedKernel.Aspire.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
