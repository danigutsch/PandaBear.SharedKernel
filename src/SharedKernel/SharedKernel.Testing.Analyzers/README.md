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

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKTEST001.severity = suggestion
dotnet_diagnostic.SKTEST002.severity = suggestion
```

## See Also

- [SharedKernel.Testing.CodeFixes](../SharedKernel.Testing.CodeFixes/README.md)
- `tests/AGENTS.md`
