# SharedKernel.Testing.Assertions

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable xUnit assertion extensions for SharedKernel test suites.

## Usage

Import `PandaBear.Testing.Assertions` for `ShouldBe` and collection assertions. Use `ExceptionAssertions.ThrowsInner<TException>` when validating wrapped failures. Keep assertions tied to observable behavior and use the comparer/precision overloads when equality semantics require them.

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

Each form also supports `ShouldThrow<T>(paramName)` for `T : ArgumentException` and checks the
parameter name, including `null`. `ShouldThrowAssignableTo<T>` is the explicitly assignable
asynchronous variant. Do not substitute it for exact exception checks. An async lambda must use
`Func<Task>` rather than `Action`, which would erase the awaited failure as `async void`.

```csharp
Action synchronous = () => service.Execute();
var exception = synchronous.ShouldThrow<InvalidOperationException>();

Func<object?> accessor = () => service.Value;
accessor.ShouldThrow<ArgumentNullException>("value");

Func<Task> asynchronous = () => service.ExecuteLater();
await asynchronous.ShouldThrow<InvalidOperationException>();
```

### Epic #6 evaluation

The API inventory for #4 found all three typed forms, their parameter-name overloads, and the
explicit asynchronous assignable variant already present. No new public delegate wrapper or
assertion overload is needed. Direct contract tests cover returned exception identity, derived
exception rejection, and parameter-name validation. There is no `Delegate`, `object`, or
`Func<ValueTask>` assertion wrapper; unsupported forms must not be silently coerced.

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
