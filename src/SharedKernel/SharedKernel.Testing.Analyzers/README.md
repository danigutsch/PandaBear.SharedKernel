# SharedKernel.Testing.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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
| `SKTEST008` | Warning | xUnit test methods should not use manual `try`/`catch`/`finally` control flow. |
| `SKTEST009` | Warning | xUnit trait metadata should use canonical constants when they exist. |
| `SKTEST011` | Suggestion | Inline adjacent one-use assertion lambdas when overload binding is unchanged. |
| `SKTEST012` | Warning | Use uniqueness assertions instead of assertion-like LINQ Single calls. |
| `SKTEST013` | Warning | Use semantic assertions or named values instead of parenthesized assertion receivers. |
| `SKTEST014` | Warning | Prevent conditional access from silently skipping ShouldBe assertions. |
| `SKTEST015` | Warning | Use the specialized Playwright wrapper for default string title assertions. |
| `SKTEST010` | Warning | Compare test activity source instances by identity. |
| `SKTEST016` | Warning | Construct explicitly named SUTs in Arrange. |

See [test assertion conventions](../../../docs/TEST_GUIDELINES.md) for migrations, exception-testing
exemptions, and the supported code-fix boundaries.

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
dotnet_diagnostic.SKTEST009.severity = warning
dotnet_diagnostic.SKTEST011.severity = suggestion
sharedkernel_testing_required_traits = Category=Smoke
sharedkernel_testing_strict_test_method_casing = false
```

`SKTEST007` is intentionally opt-in by syntax: it only checks methods that already contain a single
complete Arrange/Act/Assert marker set in line comments, with optional whitespace after `//`.
Tests without that exact marker set are not reported.
Repeated markers are not reported because multi-act tests often need local judgment before restructuring.

`SKTEST008` is intentionally syntax-bound: it reports `try` statements inside xUnit test methods. It
does not report production code or non-test helper methods.

`SKTEST009` is conservative: it reports string literals in `Trait` attributes only when exactly one
matching constant can be identified from `SharedKernel.Testing` trait constants or a project-local
`TestTraits` type in the current namespace chain.

Keep existing `SKTEST001`–`SKTEST009` diagnostics at `warning`; repository warning-as-error settings make violations fail
the build without changing analyzer descriptor severity.

`sharedkernel_testing_strict_test_method_casing` defaults to `true` and requires sentence-style
underscore names like `Creates_a_tour_when_the_request_is_valid` while allowing known terms such as
`API`, `AsyncAPI`, `HTTP2`, `SKTEST004`, `xUnit`, `IDs`, `Task`, `ValueTask`, `HttpClient`, `OpenApi`, `UI`,
`NavMenu`, `NavLink`, and `DataAnnotationsValidator`. Set it to `false` only for temporary
migration windows.

## Package boundary

This package owns test-only diagnostics. Do not move `SKTEST*` rules into production analyzer
packages, and do not add production-source conventions here. Consumers that do not build test
projects should not need this package.

## See Also

- [SharedKernel.Testing.CodeFixes](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.CodeFixes/README.md)
- `tests/AGENTS.md`

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Analyzers`. C# namespace: `PandaBear.Testing.Analyzers`. Assembly/project identity: `SharedKernel.Testing.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## SKTEST011

This suggestion applies inside xUnit facts and theories to an adjacent, single-variable lambda
local used exactly once by `PandaBear.Testing.Assertions.ShouldAssertionExtensions.ShouldThrow`
or `ShouldThrowAssignableTo`. The diagnostic and code fix share the same semantic checks.
The fix removes the local and calls the existing static extension method with the inline lambda.
Roslyn must bind that call to the exact same constructed overload before a suggestion is offered.

```csharp
Action action = () => service.Execute();
action.ShouldThrow<InvalidOperationException>();

// When overload binding is identical:
ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>(() => service.Execute());
```

`Action`, `Func<object?>`, and `Func<Task>` keep their original synchronous, property-accessor,
and asynchronous contracts. Async assertions remain awaited. Named arguments, lambda comments,
and surrounding comments are retained. Calls must be direct statements, returns, or single-local
initializers, optionally awaited. Method groups, casts, reassigned or reused locals,
intervening statements, nested functions, switch arms, conditional calls, declaration-interior
comments, directives, and overload changes receive no suggestion. In particular, a throw-only
lambda can select a different delegate overload when inlined; keep the typed local in that case.
Unrelated assertion APIs and production methods are excluded. Fix All remains unavailable.

Use `dotnet_diagnostic.SKTEST011.severity = none` to retain named delegates throughout a project,
or a narrow suppression when a particular name explains assertion intent. The default is a
non-blocking suggestion; this rule does not require an application migration.

## SKTEST010

Test `ActivityListener.ShouldListenTo` lambdas must compare the intended `ActivitySource` instance, for example
`source => ReferenceEquals(source, expectedSource)`. The analyzer reports the first read of the lambda parameter's
`ActivitySource.Name`, including helper files, object initializers, later assignments, equality and `string.Equals`.
Generated code, method groups and stored predicates are outside this rule's direct-lambda analysis. No code fix
infers a source instance from a name. Ordinary assertions on `activity.Source.Name` remain valid.
Identity isolates distinct same-name sources; tests sharing one static source must also correlate trace/parent IDs
or retain justified serialization. An intentional process-global name-subscription test needs a documented,
file-scoped `.editorconfig` exception.

## SKTEST016

Construct explicitly named `sut` or `systemUnderTest` variables in Arrange. This conservative rule analyzes semantic
xUnit Fact/Theory methods only when they contain exactly one ordered `// Arrange`, `// Act`, `// Assert` marker set.
It reports direct explicit or target-typed object creation assigned to these names in Act. Other variable names,
factory calls, bUnit `Render<T>()`, nested lambdas and local functions are outside this rule. Moving construction
can change evaluation order, so no code fix is registered.

Issues #9 and #58 both originally requested SKTEST010. SKTEST010 retains the activity-listener contract;
SUT construction uses the distinct SKTEST016 identifier.
