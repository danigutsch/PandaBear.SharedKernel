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
| `SKTEST004` | Warning | xUnit tests should not hide helper declarations inside test classes or test methods. |
| `SKTEST005` | Warning | Serial xUnit collection definitions should declare a justification. |
| `SKTEST006` | Warning | Test code should use repository assertion wrappers instead of direct xUnit assertions. |
| `SKTEST007` | Warning | Complete explicit Arrange/Act/Assert marker sets in xUnit test methods should stay ordered. |
| `SKTEST008` | Warning | xUnit test methods should not use manual `try`/`finally` cleanup blocks. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKTEST001.severity = warning
dotnet_diagnostic.SKTEST002.severity = warning
dotnet_diagnostic.SKTEST003.severity = warning
dotnet_diagnostic.SKTEST004.severity = warning
dotnet_diagnostic.SKTEST005.severity = warning
dotnet_diagnostic.SKTEST006.severity = warning
dotnet_diagnostic.SKTEST007.severity = warning
dotnet_diagnostic.SKTEST008.severity = warning
sharedkernel_testing_required_traits = Category=Smoke
sharedkernel_testing_strict_test_method_casing = false
```

`SKTEST007` is intentionally opt-in by syntax: it only checks methods that already contain a single
complete Arrange/Act/Assert marker set in line comments, with optional whitespace after `//`.
Tests without that exact marker set are not reported.
Repeated markers are not reported because multi-act tests often need local judgment before restructuring.

`SKTEST008` is intentionally syntax-bound: it only reports `try` statements with `finally` blocks
inside xUnit test methods. It does not report production code or non-test helper methods.

Keep `SKTEST*` diagnostics at `warning`; repository warning-as-error settings make violations fail
the build without changing analyzer descriptor severity.

`sharedkernel_testing_strict_test_method_casing` defaults to `true` and requires sentence-style
underscore names like `Creates_a_tour_when_the_request_is_valid` while allowing known terms such as
`SKTEST004`, `xUnit`, `IDs`, `Task`, `ValueTask`, `HttpClient`, `OpenApi`, `UI`,
`NavMenu`, `NavLink`, and `DataAnnotationsValidator`. Set it to `false` only for temporary
migration windows.

## Package boundary

This package owns test-only diagnostics. Do not move `SKTEST*` rules into production analyzer
packages, and do not add production-source conventions here. Consumers that do not build test
projects should not need this package.

## See Also

- [SharedKernel.Testing.CodeFixes](../SharedKernel.Testing.CodeFixes/README.md)
- `tests/AGENTS.md`
