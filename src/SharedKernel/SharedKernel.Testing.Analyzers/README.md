# SharedKernel.Testing.Analyzers

Analyzer project for repository-wide test conventions.

## Purpose

This Roslyn component reports diagnostics for rules that only make sense in test code and should not
 be applied to regular production projects.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKTEST001` | Warning | xUnit test methods should not use local `#pragma warning disable`/`restore` directives. |
| `SKTEST002` | Warning | xUnit test methods should follow the repository underscore naming convention. |
| `SKTEST003` | Warning | xUnit test methods should include configured required trait metadata. |
| `SKTEST004` | Warning | xUnit test classes should move helper members into dedicated helper types or local functions. |
| `SKTEST005` | Warning | Serial xUnit collection definitions should declare a justification. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKTEST001.severity = suggestion
dotnet_diagnostic.SKTEST002.severity = suggestion
dotnet_diagnostic.SKTEST005.severity = warning
```

## See Also

- [SharedKernel.Testing.CodeFixes](../SharedKernel.Testing.CodeFixes/README.md)
- `tests/AGENTS.md`
