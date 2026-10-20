# SharedKernel.Aspire.CodeFixes

Code-fix project for SharedKernel Aspire conventions.

## Purpose

This Roslyn component provides focused, safe code fixes for the diagnostics in
`SharedKernel.Aspire.Analyzers`.

## Current State

- `SKASPIRE001` can add or replace image pin calls with uncompilable placeholders so the developer
  must fill in a verified tag or bare SHA-256 digest before the code builds.

## Package boundary

This package owns fixes for `SharedKernel.Aspire.Analyzers` diagnostics only. Do not add
repository-wide style fixes, test-only fixes, or mediator fixes here.

## See Also

- [SharedKernel.Aspire.Analyzers](../SharedKernel.Aspire.Analyzers/README.md)
- [Aspire official documentation](https://aspire.dev)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Aspire.CodeFixes`. C# namespace: `PandaBear.Aspire.CodeFixes`. Assembly/project identity: `SharedKernel.Aspire.CodeFixes`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
