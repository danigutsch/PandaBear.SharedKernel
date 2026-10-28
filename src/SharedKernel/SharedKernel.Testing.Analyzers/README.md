# SharedKernel.Testing.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Analyzer project for repository-wide test conventions.

## Purpose

This Roslyn component reports diagnostics for rules that only make sense in test code and in libraries that declare
test assertions. Do not apply it to regular production projects.

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
| `SKTEST018` | Suggestion | Every xUnit test declares exactly one valid test scope. |
| `SKTEST017` | Disabled; opt-in warning | Give every public `Should*` assertion an overload that takes `userMessage`. |

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

This package owns diagnostics for test code and for libraries that declare test assertions. Do not move `SKTEST*` rules
into production analyzer packages, and do not add conventions for regular production source here. Consumers that
neither build test projects nor declare test assertions should not need this package.

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

## SKTEST017

Assertion libraries give every public `Should*` assertion an overload that takes `string userMessage`, the reason the
test expects the assertion to hold. The rule is disabled by default. Enable it only in projects that declare
assertions, for example in the project's `.editorconfig`:

```ini
[*.cs]
# Assertion libraries give every public Should* assertion a userMessage overload.
dotnet_diagnostic.SKTEST017.severity = warning
```

A method is an assertion when all of these hold:

- It is an ordinary static extension method with a `this` parameter. Members of C# 14 `extension` blocks are outside
  this rule.
- It and its containing type are public.
- Its name is `Should` followed by an uppercase letter.
- It returns neither `bool` nor `bool?`, which keeps predicates such as `ShouldHandleAsFailure` out.
- It has no `[Obsolete]` attribute and is not generated code.

Severity alone decides where the rule applies; there is no namespace, assembly, or test-framework heuristic.

The overload takes `string userMessage` after the other parameters and before a trailing `CancellationToken`. The
parameters before it keep their types, ref kinds, attributes, and `this`, but lose `params` and default values, so a
`params` parameter becomes a plain array. A trailing token keeps its default. The name, type parameters, constraints,
and return type stay the same. For example, these assertions take these overloads:

```csharp
public static void ShouldBeOneOf<T>(this T actual, params T[] expected)
public static void ShouldBeOneOf<T>(this T actual, T[] expected, string userMessage)

public static Task ShouldHaveStatus(this HttpResponseMessage actual, HttpStatusCode expected,
    string? reason = null, CancellationToken cancellationToken = default)
public static Task ShouldHaveStatus(this HttpResponseMessage actual, HttpStatusCode expected,
    string? reason, string userMessage, CancellationToken cancellationToken = default)
```

An assertion with an optional trailing token, as in the second example, and its overload then both have optional
parameters. PublicApiAnalyzers reports such a pair as RS0026 while either one is unshipped, and a required token would
make RS0027 report the assertion instead, so no overload shape satisfies both rules. Projects that enforce RS0026
suppress it for such pairs.

An assertion is covered, and not reported, when either of these holds:

- It has a `string` parameter named `userMessage` after its `this` parameter, optional or not, as `ShouldBeTrue` and
  `ShouldBeFalse` have.
- Its type declares a same-named public extension overload with as many type parameters, whose parameter types and ref
  kinds match the overload shape and whose parameter at the message position is named `userMessage`. Types compare as
  C# signatures do, so `dynamic` matches `object` and tuple element names do not matter. Other parameter names,
  type-parameter names, constraints, return type, `params`, and default values do not matter either.

A message after the trailing token, an overload in another type, or an overload that is not a public extension method
does not cover an assertion. A partial assertion is reported once, at its definition.

The diagnostic message names the reason when the code fix cannot add the overload:

| Condition | Reason |
| --- | --- |
| Another overload already has the overload's parameter types and is not a public extension method with `userMessage` at the message position | The overload would not compile (CS0111). |
| A partial method | The code fix does not copy partial declarations. |
| A parameter named `userMessage` that does not cover the assertion, such as the `this` parameter | The overload's message parameter would have the same name. |
| A `ref`, `in`, or `out` parameter, a ref struct, pointer, or function-pointer parameter, a parameter of a type parameter that allows ref structs, or `__arglist` | A lambda cannot capture or forward it. |
| A by-reference return, a ref struct or a type parameter that allows ref structs, an iterator, `async void`, an awaitable other than `Task` and `Task<T>`, an `IEnumerable`, `IEnumerator`, `IAsyncEnumerable<T>`, or `IAsyncEnumerator<T>` result, or a delegate result | The helper cannot forward the result. A `ValueTask` lambda would bind to `Func<T>`, so the message would be lost without an error, and a sequence or a delegate can fail after the helper has returned it. An awaitable is found through base types, interfaces, and type-parameter constraints. |
| A `[NotNull]` parameter | Its null state does not flow out of a lambda. |
| A `[DoesNotReturn]` method | The forwarder returns, which CS8763 reports. |
| An `[AllowNull]` parameter whose type is a type parameter without a `struct` constraint | A maybe-null argument makes the forwarded call infer a nullable type argument, which the compiler reports. |
| A `[SuppressMessage]` or `[UnconditionalSuppressMessage]` for a rule other than `SKTEST017` | The suppression does not cover the overload, and a rule that inspects the signature would report it. |
| An overload with the same parameters plus a trailing `StringComparison`, `IFormatProvider`, or `CultureInfo` | Forwarding to the assertion would omit the parameter that CA1307, CA1305, or CA1304 requires. |
| An optional parameter before the message, without an optional trailing `CancellationToken` | Those parameters would become required in the overload, which RS0027 reports on the assertion. |
| No optional parameter, and a public overload of any arity that has optional parameters and no more parameters than the overload | That overload would no longer have the most parameters, which RS0027 reports on it. |
| Another overload with the overload's parameter types except at the message position, where it takes a string through a type parameter or an implicit conversion, or a last `params` array whose element type both a string and the array convert to, such as `object` | Existing calls that pass a string there would bind to the overload without a diagnostic. |

Write the `[NotNull]`, CA1307, CA1305, and CA1304 overloads by hand: assign the checked value back to the `[NotNull]`
parameter, and forward to the overload with the comparison, format provider, or culture that the assertion uses. Give a
hand-written overload of a suppressed assertion its own justified suppression. Keep a same-signature assertion and
suppress its diagnostic with a justification, or rename one of the overloads in a breaking change.

The code fix adds the overload after the assertion with the expression body
`WithUserMessage(userMessage, () => actual.Name(arguments))`, and writes type arguments only where inference cannot
supply them. It copies the assertion's documentation, adds the `userMessage` condition to its `ArgumentNullException`
exception element or adds that element, and adds a `userMessage` parameter line when the assertion documents its
parameters; CS1573 reports a comment that documents only some of them. It needs an accessible static `WithUserMessage`
method with the four shapes `(string, Action)`, `(string, Func<T>)`, `(string, Func<Task>)`, and
`(string, Func<Task<T>>)`, for example through `using static`. Without one, the diagnostic is still reported but no
fix is offered. The fix is offered only when the forwarded call binds to the assertion, the `WithUserMessage` call binds
to a static method whose first parameter is a `string` and, for a `Task` or `Task<T>` result, whose delegate returns a
task, no extension `GetAwaiter` makes another result awaitable, the project gains no compiler warning or error, and the
assertion is covered afterwards. The fix assumes that the assertion completes its checks before it returns; write the
overload by hand for any other lazily evaluated result.

Fix All is unavailable. Apply the fix to a whole project with the code-fix runner, then add the public API lines that
RS0016 reports:

```bash
dotnet run --project tools/SharedKernel.Testing.CodeFixRunner -c Release -- <project> --diagnostic SKTEST017
dotnet format analyzers <project> --diagnostics RS0016 --severity warn
```

## SKTEST018

Every xUnit test declares the seam it exercises. A test satisfies the rule when its method, its class or an enclosing
or base class, or its assembly carries `[TestScope(TestScope.X)]` (`SharedKernel.Testing.Xunit`) or
`[Trait("Scope", "<value>")]`. The nearest level that declares a scope wins, so a method scope overrides a class scope.

The rule reports three cases at the test method:

- `missing`: no scope at any level.
- `unknown`: the winning scope is outside the allowed set, including an undefined `TestScope` value.
- `conflicting`: two different scopes at the winning level. The same scope declared twice is not a conflict.

The allowed set is the `TestScope` values (`unit`, `behavior`, `component`, `contract`, `ui-integration`,
`integration`, `system`, `architecture`). Narrow it per repository:

```ini
[*.cs]
sharedkernel_testing_allowed_scopes = unit;integration
```

The rule reports at suggestion severity so adoption can be staged. Promote it once the repository is fully tagged:

```ini
[*.cs]
dotnet_diagnostic.SKTEST018.severity = warning
```

The code fix, offered only for a missing scope, adds one action per allowed scope to the test class. It writes the typed
attribute when `PandaBear.Testing.TestScopeAttribute` is referenced and `[Trait("Scope", ...)]` otherwise.
