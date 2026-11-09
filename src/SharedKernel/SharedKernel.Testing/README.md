# SharedKernel.Testing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable test traits, environment scopes, and temporary-directory lifetimes.

## Trait taxonomy

- `TestTraitNames` owns cross-project trait names such as `Scope`, `Area`, `Category`, `Host`, and
  `Surface`.
- `TestTraitValues` owns cross-project values reused by multiple bounded-context test projects. Scope and host
  traits come from `TestScope` and `TestHost` (the `[TestScope]` and `[TestHost]` attributes, also valid on an assembly);
  test projects keep only their own component and capability values in a local `TestTraits`.
- `SharedKernelTestTraitNames` owns SharedKernel package-test names and the neutral `Unit` scope.
- `TestScope` enumerates the test seams (`Unit`, `Behavior`, `Component`, `Contract`, `UiIntegration`,
  `Integration`, `System`, `Architecture`) and `ToTraitValue()` maps each to its `Scope` trait value.
  `TestHost`, `TestFocus` and `TestPriority` do the same for `Host`, `Focus` and `Priority`.
  `SharedKernel.Testing.Xunit` provides the matching attributes.
- Project-specific values belong in the owning test project's `TestTraits` type.

Use constants in `Trait` attributes whenever they exist. `SharedKernel.Testing.Analyzers` reports
hardcoded trait literals through `SKTEST009` when a single safe replacement can be identified.

## Serial test justification

Use `SerialTestJustificationAttribute` on xUnit collection definitions that disable parallelization.
`SKTEST005` enforces a non-empty reason.

## Test doubles

- Streams: `BlockingReadStream`, `GatedCountingReadStream`, `RecordingWriteStream`,
  `TrackingReadStream` and `UnsizedStream` cover blocking, counting, gating, unknown-length and disposal-tracking cases.
- Metrics: `MeterCollector` is an `IMeterFactory` that records the long measurements of its meters (pass a meter
  name to restrict it); `ThrowingMeterFactory` fails every `Create` call.
- Processes and Git: `TestProcess.Run` runs a process and captures its output; `TestGit` runs Git without the user's
  configuration and commits a directory (`CommitAll`, `Commit`); `TemporaryGitRepository` owns an isolated repository
  with `Initialize`, `Commit`, `Tag` and `RunGit`.
- Time: `CountingTimeProvider` wraps a `TimeProvider` (the system clock by default) and counts how often the current UTC time is
  read (`Reads`, `Reset`), to prove that an operation reads the clock once. For a fixed or movable clock use
  `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` (`SetUtcNow`, `Advance`, `SetLocalTimeZone`) and pass it
  as the inner provider; no helper here duplicates it.
- Ports and cleanup: `LoopbackPort.GetFree` returns an unused loopback port; `TrackingCleanupAction` and
  `TrackingAsyncDisposable` record the order of cleanup and disposal and can fail on demand.
- Executables: `FakeExecutable` writes POSIX shell scripts (`WriteScript`, `WriteMarker`, `Quote`);
  `RecordingFakeExecutable` also records arguments, environment and process ID.
- File system: `FileSystemProbes` reports `CanCreateSymbolicLinks`, `CanDenyAccess` and `IsCaseSensitive` for
  `SkipUnless` conditions. Use `TemporaryDirectory` for every temporary directory lifetime.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing`. C# namespace: `PandaBear.Testing`. Assembly/project identity: `SharedKernel.Testing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Temporary resources

```csharp
using PandaBear.Testing;

using var directory = new TemporaryDirectory();
File.WriteAllText(Path.Combine(directory.Path, "fixture.txt"), "fixture");
using var variable = new EnvironmentVariableScope("EXAMPLE_SETTING", "test");
```

`EnvironmentVariableScope` restores the previous value, including null when originally unset.
Dispose nested scopes in reverse order and serialize tests that modify process-wide environment
variables. The helper does not provide concurrency isolation.

`TemporaryDirectory` uses the platform temporary-directory API. Disposal removes nested read-only
files, avoids traversing symbolic links, and accepts an already removed root. Cleanup failures
propagate. Keep fixture-specific file writers in the owning tests.

## Call proxy

`CallProxy.Create<IStore>(realStore, hooks => ...)` wraps a service so a test can hold, fail, follow up or replace a call without a
decorator class per service. Hooks are chosen by method name and apply to methods that return `Task`, `Task<T>`, `ValueTask`,
`ValueTask<T>`, a value, or nothing:

```csharp
var store = CallProxy.Create<IOrderStore>(real, hooks => hooks
    .Before(nameof(IOrderStore.Find), call => gate.Wait((Guid)call.Args[0]!))   // hold until released, or throw to fail
    .After(nameof(IOrderStore.Find), call => DeleteRow(call.Result))            // follow up with call.Result set
    .Returns(nameof(IOrderStore.EmailExists), false));                          // replace the result, real service not called
```

Exceptions of the real service arrive unwrapped. The service must be a public interface (the runtime generates the proxy type), not an
abstract class. Hooks are chosen by method name only, so overloads (and same-named methods of different interfaces) share their hooks.
Hooks of a synchronous method are waited for on the calling thread, so keep them short. `out` and `ref` values written by the real
service reach the caller, and `call.Args` shows them to after hooks. A `Returns` value that does not fit the method's result type (for
example `null` for an `int`) throws an `InvalidOperationException` naming the method when the call is made.

`Create` validates the registrations after `configure` ran, so a test never passes while believing a call was stubbed. Hooks and
replacements are keyed by name and apply to every overload of that name. It throws:

- `ArgumentException` when a `Before`, `After` or `Returns` name matches no method of the service or its inherited interfaces; the
  message lists the unknown names and the available method names.
- `InvalidOperationException` ("Returns cannot be used for 'Save', which returns no value") when `Returns` targets a name whose methods
  all return `Task`, `ValueTask` or `void`.
- `InvalidOperationException` when every value-returning method of a `Returns` name has `out` or `ref` parameters, which cannot be
  stubbed; use `After` or `Before` and let the real call run.

A `Returns` name is valid as long as at least one overload returns a value without `out`/`ref` parameters.

It fits a test that wraps a real implementation and needs one targeted hold, failure, follow-up or replaced result. It does not replace a
fake that implements the whole interface with typed recorded state, a service registered by type, or delegate-based fakes.
