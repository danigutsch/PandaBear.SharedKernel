# SharedKernel.Testing.Assertions

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable xUnit assertion extensions for SharedKernel test suites.

## Usage

Import `PandaBear.Testing.Assertions` for `ShouldBe` and collection assertions. Use `ExceptionAssertions.ThrowsInner<TException>` when validating wrapped failures. Keep assertions tied to observable behavior and use the comparer/precision overloads when equality semantics require them.

Pass the reason that an assertion holds as `userMessage`. Every `Should*` assertion takes it after its other arguments, except the `ShouldThrow<T>` forms without `paramName` described under [exception delegate contracts](#exception-delegate-contracts). `ShouldBeTrue` and `ShouldBeFalse` take it as an optional argument and report xUnit's own `TrueException` or `FalseException`, whose message is `userMessage` alone when one is given, without xUnit's expected and actual lines. The other assertions report a failure as an `XunitException` whose message starts with `userMessage` and whose inner exception is the original failure, including failures that are not xUnit assertions, such as the `XmlException` of `ShouldBeWellFormedHtmlDocument`. `ArgumentException`, `OperationCanceledException`, and dynamic skips pass through unchanged, and a null `userMessage` throws `ArgumentNullException` before the assertion runs. To assert the specific xUnit exception type that an assertion throws, such as `EqualException`, use the overload that takes no message.

Pass the values of a `params` assertion as an array to add a message, and name the argument (`userMessage: "..."`) when the argument before it is also a string. When `T` is `object`, `IEnumerable`, or `ICloneable`, the types that both a string and an array of `T` convert to, a `ShouldBeOneOf` or `ShouldBeEquivalentTo` call with an array and a string binds to the message overload, so the string is the message rather than an expected value. A `null` or `default` first value followed by one string is also taken as the array and the message when a string converts to `T` and `T` is not `string`, such as `object`, and is ambiguous (CS0121) when the values are strings, as in `ShouldContainInOrder` and `ShouldHaveValidationError`. Pass such values as an array, such as `value.ShouldBeOneOf([null, "Ready"])`, or cast the null, such as `(object?)null`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Assertions`. C# namespace: `PandaBear.Testing.Assertions`. Assembly/project identity: `SharedKernel.Testing.Assertions`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Exception delegate contracts

Use the existing typed `ShouldThrow<T>` overloads:

| Delegate | Intended use | Contract |
| --- | --- | --- |
| `Action` | Synchronous operation | Throws exactly `T` and returns that exception. |
| `Func<object?>` | Property or value accessor | Throws exactly `T` and returns that exception. |
| `Func<Task>` | Asynchronous operation | Await the returned task; throws exactly `T` and returns that exception. |

Each form also supports `ShouldThrow<T>(paramName)` and `ShouldThrow<T>(paramName, userMessage)`
for `T : ArgumentException` and checks the parameter name, including `null`. The forms without
`paramName` take no message, because their `(delegate, string)` signature belongs to the
`paramName` overload: for an `ArgumentException` type, a string passed there is checked as the
parameter name. `ShouldThrowAssignableTo<T>` is the explicitly assignable asynchronous variant.
Do not substitute it for exact exception checks. An async lambda must use `Func<Task>` rather than
`Action`, which would erase the awaited failure as `async void`.

```csharp
Action synchronous = () => service.Execute();
var exception = synchronous.ShouldThrow<InvalidOperationException>();

Func<object?> accessor = () => service.Value;
accessor.ShouldThrow<ArgumentNullException>("value");

Func<Task> asynchronous = () => service.ExecuteLater();
await asynchronous.ShouldThrow<InvalidOperationException>();
```

### SKTEST017 suppressions

The three `ShouldThrow<T>` forms without `paramName` suppress SKTEST017 with a justification. This
record follows the [warning policy](../../../docs/ci/warning-policy.md), except that it names review
triggers instead of an expiry or review date.

| Field | Record |
| --- | --- |
| Producer and rule | `SKTEST017` from `SharedKernel.Testing.Analyzers`. |
| Scope | One method-level `[SuppressMessage]` on each of `ShouldThrow<T>(this Action)`, `ShouldThrow<T>(this Func<object?>)`, and `ShouldThrow<T>(this Func<Task>)` in `ShouldAssertionExtensions.cs` of this project. |
| Baseline | 3, a fixed set. Another suppression needs its own record. |
| Rationale and evidence | `ShouldThrow<T>(delegate, string? paramName)` already has the `(delegate, string)` signature that a `userMessage` overload needs, and constraints are not part of a signature, so the overload would not compile (CS0111). SKTEST017 names the colliding overload as the reason it offers no code fix. |
| Owner | SharedKernel maintainers. |
| Remediation | Rename the `paramName` overloads in a breaking change, remove the suppressions, and add the three overloads with the code fix. |
| Review | At the first stable API review, and whenever the `ShouldThrow` overloads or SKTEST017 change. |
| Findings outside the exception | Each suppression covers only its own method. SKTEST017 is a warning in this project, so warnings-as-errors fails the build for any other public assertion without a `userMessage` overload. The repository convention tests in `AssertionLibraryAnalyzerTests` keep the rule enabled in the three assertion libraries: they check each project's opt-in property and `.editorconfig` severity, and the testing analyzer references that `src/Directory.Build.props` adds for the opt-in. |

### Epic #6 evaluation

The three typed forms, their `paramName` overloads with and without `userMessage`, and the explicit
asynchronous assignable variant cover delegate exception assertions. Direct contract tests cover
returned exception identity, derived exception rejection, and parameter-name validation. There is
no `Delegate`, `object`, or `Func<ValueTask>` assertion wrapper; unsupported forms must not be
silently coerced.

The #5 research supports inline lambdas when the call states the action clearly, and named,
typed delegates when they explain intent or preserve overload selection. Captured variables are
captured by reference, so moving lambdas across statements or into a nested function requires
more reasoning than a mechanical simplification. #7 therefore adds only the conservative
`SKTEST011` suggestion described in the analyzer README. It does not add unrelated assertion
rules or change existing exception semantics.

Sources checked on 2026-10-21:

- [xUnit v3 synchronous exception assertions](https://api.xunit.net/v3/3.0.0/v3.3.0.0-Xunit.Assert.Throws.html): exact exception types, typed actions, property accessors, and parameter names.
- [xUnit v3 asynchronous exception assertions](https://api.xunit.net/v3/3.0.0/v3.3.0.0-Xunit.Assert.ThrowsAsync.html): awaited `Func<Task>` and exact exception types.
- [C# lambda expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-expressions): target delegate conversion, overload inference, and captured variables.

## Results and entity identities

Use `result.ShouldBeSuccess()` to verify success; for `Result<T>` it returns the value.
Use `result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid)` to verify both the
status and stable code and retrieve the error. Then assert exact ordered field messages with
`error.ShouldHaveValidationError("Id", "Id is required.")`. Both result assertions reject
uninitialized results. Keep the Results library's own status and invariant tests explicit.

`first.ShouldHaveGeneratedIdentitySemantics(second, different)` verifies generated GUID identity
equality, hashes, collection membership, and empty identities. It mutates three test-owned instances
through their `Id` setter, including private init setters. This test-only helper retains reflected
property access because the existing domain models deliberately hide identity mutation. Generic
metadata annotations preserve the property boundary; this package does not promise NativeAOT support.
Use `id.ShouldBeUuidV7()` for generated identifier version and standard UUID variant checks.
