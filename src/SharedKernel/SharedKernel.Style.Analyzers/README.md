# SharedKernel.Style.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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
| `SKSTYLE009` | Warning | Methods returning `SharedKernel.Results.Result` or `Result<T>`, including async `Task` or `ValueTask` wrappers, must be able to return a failure `Result`; use a non-`Result` return type when every reachable return is successful. Overrides and interface implementations are excluded. |
| `SKSTYLE010` | Warning | Methods returning `SharedKernel.Results.Result<T>` only as `Ok` or `NotFound` should return `Option<T>`. |
| `SKSTYLE011` | Disabled; opt-in warning | Review whole-input reads in size-sensitive production scopes. |
| `SKSTYLE012` | Warning; requires catalog | Replace exact registered C# string literals with the named canonical field. |
| `SKSTYLE013` | Warning; requires catalog | Correct malformed or ambiguous governed-literal metadata. |

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

Existing `SKSTYLE001`–`SKSTYLE010` descriptors default to `warning`. `SKSTYLE011` is disabled by default and becomes a warning when explicitly enabled. Repository `.editorconfig` can stage cleanup-heavy
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

- [SharedKernel.Style.CodeFixes](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.CodeFixes/README.md)
- [SharedKernel.Aspire.Analyzers](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Aspire.Analyzers/README.md)
- `docs/CODING_GUIDELINES.md`

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Style.Analyzers`. C# namespace: `PandaBear.Style.Analyzers`. Assembly/project identity: `SharedKernel.Style.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## SKSTYLE011

This section defines the library IO policy, whole-read diagnostic, and safe-code-fix assessment.
The rule lives in the existing
Style analyzer package; no IO analyzer package, runtime primitive, or dependency is necessary.
Application adoption and input-size policy remain with each application.

### Policy and detection

Enable the rule only in production scopes where untrusted or potentially large input must have
an enforced size budget. It reports invocations of `System.IO.File.ReadAllText`, `ReadAllBytes`,
and their async variants, and `TextReader`/`StreamReader.ReadToEnd` and `ReadToEndAsync`.
Encoding and cancellation-token overloads are included. Aliases, static imports, conditional
calls, inherited methods, and virtual overrides use semantic method identity.

The warning asks the author to enforce a limit during reading or document an already bounded
source. Cancellation and asynchronous execution do not impose a size limit. A pre-read
`FileInfo.Length` or content-length check alone does not prove the bytes consumed remain bounded.
Choose bytes versus characters, the maximum accepted size, rejection behavior, cancellation,
and stream/temp-file ownership explicitly. The existing repository-tool
[`BoundedHttpContentReader`](../../../tools/SharedKernel.RepoConfig.Tool/BoundedHttpContentReader.cs)
illustrates checking the accumulated byte count while reading; it is not a new public API.

```ini
# Enable only the selected production import scope.
[src/**/Imports/*.cs]
dotnet_diagnostic.SKSTYLE011.severity = warning

# Keep test fixtures outside that rollout.
[tests/**/*.cs]
dotnet_diagnostic.SKSTYLE011.severity = none
```

```csharp
// Reported in an enabled production scope: allocation follows external input size.
var text = await reader.ReadToEndAsync(ct);
var bytes = File.ReadAllBytes(path);

// No whole-input diagnostic: bounded incremental buffer; still check actual read count.
var count = await stream.ReadAsync(buffer.AsMemory(), ct);
```

### Exclusions and limits

The rule is disabled unless the consumer enables it using standard diagnostic configuration.
Generated code is excluded. Test-only scopes use ordinary `.editorconfig` exclusions; there is
no assembly-name, filename, or test-framework heuristic that silently exempts production code.
Direct `StringReader` methods consume an existing string and are excluded. Incremental reads,
`ReadLine`, lazy `File.ReadLines`, unresolved/dynamic calls, method groups, and unrelated
same-named methods are outside this diagnostic.

This is a review rule, not a proof of missing validation. It does not trace stream construction,
interprocedural limits, runtime types, or prior copies into bounded buffers. A `StreamReader`
over a safely limited in-memory buffer can still be reported. Retain the existing implementation
and add a narrow standard suppression with evidence of its enforced bound, for example:

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("IO", "SKSTYLE011",
    Justification = "The input was copied with an enforced byte limit before this reader was created.")]
```

Do not suppress an unknown bound. Whole-input APIs in trusted configuration/tooling code may be
excluded by a reviewed file scope or justified suppression. Existing library callers keep their
behavior until a consumer deliberately enables the rule. No application migration is performed here.

### Rule selection and code-fix decision

| Concern | Decision | Reason |
| --- | --- | --- |
| Async stream buffer overloads | Reuse SDK CA1835 | Already covers memory-based overload guidance. |
| Ignored partial-read counts | Reuse SDK CA2022 | Already covers unchecked `Read`/`ReadAsync` results. |
| Missing cancellation forwarding | Reuse SDK CA2016 | Already covers eligible token forwarding. |
| Disposable ownership | Reuse SDK CA2000 | Existing disposal analysis should remain enabled/configured. |
| Whole-input allocation in selected scopes | Add SKSTYLE011 | The rules above do not establish a size budget for whole-input materialization. |
| General synchronous IO in async methods | No additional diagnostic | Requires a separate scope and semantic review; changing to async still leaves unbounded allocation. |

For the chosen diagnostic, no mechanical rewrite can infer a correct size limit, unit, parser,
error contract, or ownership policy. Replacing a whole read with a truncated read loses data;
adding an arbitrary limit changes accepted input; replacing it with an async read leaves the
allocation unbounded. Regression tests enforce the no-code-fix decision by rejecting individual
and bulk actions for these invocations. There is no placeholder fix or
empty IO code-fix package. Existing style code fixes remain available for their own diagnostics.

Sources reviewed on 2026-10-21:

- [CA1835](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1835), [CA2022](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2022), [CA2016](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2016), and [CA2000](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2000) define the existing SDK responsibilities above.
- [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer), [AsyncFixer](https://github.com/semihokur/AsyncFixer), [IDisposableAnalyzers](https://github.com/DotNetAnalyzers/IDisposableAnalyzers), and [ErrorProne.NET](https://github.com/SergeyTeplyakov/ErrorProne.NET) are research references for analyzer scope and conservative fixes, not dependencies or copied implementations.

## Governed literals

The opt-in catalog is [`GovernedLiterals.txt`](GovernedLiterals.txt), also packed under
`governance/` in this analyzer package. It contains governance metadata, not runtime constants.
No new package or dependency is introduced. Reference the existing owner package before replacing
literals: `PandaBear.SharedKernel.AspNetCore` owns `HostEnvironmentVariableNames`;
`PandaBear.SharedKernel.MalwareScanning.ClamAv` owns `ClamAvMalwareScannerConfigurationKeys`.
The host names are web-host integration values; Domain has no hosting dependency. Existing ClamAV
callers already use their provider-owned constants. There were no direct C# host-name callers to
migrate in the library. Application callers and policy remain in their application repository.

To adopt, copy the catalog to a consumer-owned location, review its owners and package prerequisites,
and pass it as an additional file. Use the exact basename `GovernedLiterals.txt`:

```xml
<ItemGroup>
  <AdditionalFiles Include="governance/GovernedLiterals.txt" />
</ItemGroup>
```

Each nonblank, noncomment line has three pipe-separated columns:
`literal|assembly simple name|fully.qualified.Type.Field`. Whitespace around columns is trimmed;
values and symbols are case-sensitive. Lines beginning with `#` are comments. The format has no
escaping for pipes or newlines. Multiple additional files with this basename are merged and must
remain unambiguous. No additional file means no governed-literal diagnostics. Invalid input emits
`SKSTYLE013` instead of silently skipping enforcement. Standard `.editorconfig` severity and narrow,
justified Roslyn suppressions apply; applications own their rollout and extra entries.

`SKSTYLE012` compares decoded ordinary, verbatim, escaped, and raw C# string literal tokens exactly.
It does not infer interpolated strings, concatenate consumer expressions, scan configuration formats
(JSON, YAML, shell, Markdown), or govern arbitrary routes, roles, claims, or test values. Generated
code and the exact canonical `const string` declaration in its registered assembly are exempt;
another field in the same type is not exempt. A symbol reference is allowed. The rule does not
prove repository-wide uniqueness and offers no automatic import or package-reference code fix.
SharedKernel passes its catalog only to production source projects; test data remains outside
that rollout.

`GovernedLiteralRepositoryTests` separately checks all packaged library projects in the solution, including
reusable testing packages and explicitly linked source. The build records evaluated compiler inputs,
resolved references, global usings, and conditional compilation symbols; excluded files, application
samples, benchmarks, and test data are outside the inventory. It uses Roslyn constant values,
so literal duplicates, concatenated duplicates, and redirected aliases across projects are checked.
Local `const string` declarations with governed values are also rejected; compatibility exceptions
apply only to named fields. Generated-code attributes on enclosing types or methods exempt nested
declarations as well.
Every registered canonical field must exist exactly once with its registered value.
The repository selector conservatively requests full validation for packaged-library or guard-test
changes so clean CI always builds the complete inventory and runs the guard. Unresolved
string constants fail closed. Test projects and generated declarations are excluded. Unregistered values
remain unrestricted. Run the guard with:

```sh
dotnet build SharedKernel.slnx -c Release --no-restore
dotnet test --project tests/SharedKernel.Style.Analyzers.Tests/SharedKernel.Style.Analyzers.Tests.csproj -c Release --no-build --no-restore -- --filter-class '*GovernedLiteralRepositoryTests'
```

A reviewed compatibility exception adds a fourth nonempty justification column to a row naming
the exact exception field and assembly, using the same literal as its canonical row. This permits
that field in the uniqueness guard; it does not create another canonical replacement or exempt
a direct literal from `SKSTYLE012`. Prefer a redirected alias for a published API compatibility
exception. Stale exceptions fail the guard. The library currently needs no compatibility exceptions;
fixtures cover documented aliases and reject missing justifications. Consumers can reuse this
catalog contract and the guard test pattern, while owning their own compilation inventory and
application exceptions.
