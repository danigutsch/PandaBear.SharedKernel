# SharedKernel.Testing.Analyzers

Analyzer project for repository-wide test conventions.

## Purpose

This Roslyn component reports diagnostics for rules that only make sense in test code and should not
 be applied to regular production projects.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKTEST001` | Warning | xUnit test methods should not use local `#pragma warning disable`/`restore` directives. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKTEST001.severity = suggestion
```

The repository currently stages `SKTEST001` as a suggestion for rollout.
It can be raised to warning or error after the existing codebase is cleaned up.

## See Also

- [SharedKernel.Testing.CodeFixes](../SharedKernel.Testing.CodeFixes/README.md)
- `tests/AGENTS.md`
