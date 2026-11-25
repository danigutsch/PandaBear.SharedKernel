# SharedKernel.Testing

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable test traits, environment and culture scopes, temporary-directory lifetimes, repository-file lookup, process fakes, and source-generated call proxies.

## Trait taxonomy

- `TestTraitNames` owns cross-project trait names such as `Scope`, `Area`, `Capability`, `Category`, `Host`, and
  `Surface`.
- `TestTraitValues` owns cross-project values reused by multiple bounded-context test projects. Scope and host
  traits come from `TestScope` and `TestHost` (the `[TestScope]` and `[TestHost]` attributes, also valid on an assembly);
  test projects keep only their own component and capability values in a local `TestTraits`.
- Existing filters keep their keys when adopting `TestTraitValues`. For example, `Category=contract`
  and `Scope=contract` select different traits. `IntegrationEventTransport` can label either a
  category or a capability without requiring a reference to hosted test infrastructure.
- `TestScope` enumerates the test seams (`Unit`, `Behavior`, `Component`, `Contract`, `UiIntegration`,
  `Integration`, `System`, `Architecture`) and `ToTraitValue()` maps each to its `Scope` trait value.
  `TestHost`, `TestFocus` and `TestPriority` do the same for `Host`, `Focus` and `Priority`.
  `SharedKernel.Testing.Xunit` provides the matching attributes.
- Use `[TestCapability(...)]` to expose a named behavior as a `Capability` trait. Define its value in the owning test project.
- Project-specific values belong in the owning test project's `TestTraits` type.

Use constants in `Trait` attributes whenever they exist. `SharedKernel.Testing.Analyzers` reports
hardcoded trait literals through `SKTEST009` when a single safe replacement can be identified.

### Migrating filters from the previous preview

`TestTraitNames` is the shared owner of trait keys. Select a test's actual seam through
the `Scope` key, such as `Scope=unit` or `Scope=system`. Use the following
keys for existing labels.

| Previous key | Current key |
| --- | --- |
| `TestScope` | `Scope` with a lower-case seam, such as `unit` |
| `TestComponent` | `Area` |
| `TestCapability` | `Capability` |
| `TestCategory` | `Category` |
| `TestTheory` | `Category`, which also carries named law and semantics labels |

## Serial test justification

Use `SerialTestJustificationAttribute` on xUnit collection definitions that disable parallelization.
`SKTEST005` enforces a non-empty reason.

## Test doubles

- Streams: `BlockingReadStream`, `GatedCountingReadStream`, `GatedChunkReadStream`, `ThrowingReadStream`,
  `DisposalTrackingStream`, `RecordingWriteStream`, `TrackingReadStream` and `UnsizedStream` cover blocking, counting, gating,
  failing, unknown-length and disposal-tracking cases. See [Streams for cancellation and disposal tests](#streams-for-cancellation-and-disposal-tests).
- Captured output: `InvariantStringWriter` is a `StringWriter` that ends lines with `"\n"` and formats with the invariant
  culture, so text captured from a command-line tool or console is the same on every operating system. `SKTEST022`
  reports a test `StringWriter` that keeps the platform line ending. See [captured output](../../../docs/TEST_GUIDELINES.md#captured-output).
- Metrics: `MeterCollector` is an `IMeterFactory` that records the long measurements of its meters (pass a meter
  name to restrict it); `ThrowingMeterFactory` fails every `Create` call.
- Processes and Git: `TestProcess.Run` runs a process and captures its output; `TestGit` runs Git without the user's
  configuration and commits a directory (`CommitAll`, `Commit`). It starts Git from an absolute `PATH` directory as
  `GitStartInfo` of `PandaBear.SharedKernel.Processes` describes it, without a prompt or the host's tokens and inherited
  `GIT_*` variables (pass a variable a test needs in `environment`), with bounded output, and stops it after 30 seconds.
  It turns automatic maintenance off (`maintenance.auto=false`, `gc.auto=0`), so no detached `git maintenance` keeps
  writing to `.git` after a commit returned while the test reads or deletes the repository; `TemporaryGitRepository` owns an isolated repository
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

## Streams for cancellation and disposal tests

Every stream below except `GatedCountingReadStream` and `DisposalTrackingStream` is non-seekable and read-only. Signals are tasks
completed with `RunContinuationsAsynchronously`, so a test awaits them instead of sleeping; give each wait a bounded timeout only as a
failure guard.

| Stream | Behaviour | Observe |
| --- | --- | --- |
| `BlockingReadStream(prefix, remainder)` | Serves `prefix`, blocks the next read until `Release()`, its token fires, or disposal; then serves `remainder`. `new BlockingReadStream()` blocks the first read. | `ReadStarted`, `Blocked`, `BlockedReadCancelled`, `BytesRead`, `IsDisposed`, `Disposed` |
| `GatedChunkReadStream(content, chunkSize)` | Serves at most `chunkSize` bytes per read and holds every read until `ReleaseRead()` (one read) or `ReleaseAll()`. | `WaitForPendingRead(ct)`, `BytesRead`, `ReadCount`, `LargestRequestedRead`, `IsDisposed` |
| `GatedCountingReadStream(content)` | Seekable; holds reads until a single `Release()`. | `ReadStarted`, `BytesRead` |
| `ThrowingReadStream(prefix, exception)` | Serves `prefix`, then every read throws `exception` (asynchronous reads return a faulted task). | `ReadCount`, `BytesRead`, `IsDisposed` |
| `TrackingReadStream(content, seekable)` | Serves `content`, at most `MaxBytesPerRead` bytes per read. | `ReadCount`, `BytesRead`, `IsDisposed` |
| `ReadObservingStream(inner.ReadAsync, observeRead)` | Delegates asynchronous reads to a borrowed stream and leaves it open on disposal. | The requested and returned size of each successful read. |
| `DisposalTrackingStream(inner)` | Delegates to `inner` and disposes it the same way (synchronously or asynchronously). | `IsDisposed`, `Disposed`, `DisposeCount`, `AsyncDisposeCount`, `InnerStream` |

Asynchronous reads honour their cancellation token while held. Synchronous reads cannot be cancelled: they wait until a release or
disposal, so start them on a dedicated thread. Disposal ends a held read with `ObjectDisposedException`. `CopyTo` and `CopyToAsync` write
the bytes served before a block or failure to the destination first.

```csharp
// The handler must cancel the upstream read and dispose the stream when the client disconnects.
var body = new BlockingReadStream(prefix: "i"u8.ToArray(), remainder: []);
var tracked = new DisposalTrackingStream(body);
// ... hand `tracked` to the code under test, read the first byte, then disconnect ...
await body.Blocked.WaitAsync(TimeSpan.FromSeconds(10), ct);
await tracked.Disposed.WaitAsync(TimeSpan.FromSeconds(10), ct);
body.BlockedReadCancelled.ShouldBeTrue();
```

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

## Cleanup that keeps every failure

`TestCleanup.DisposeAll(operationFailure, resources...)` disposes every resource that is not `null`, in order, even when one fails, and
`TestCleanup.Run(operationFailure, actions...)` does the same for cleanup actions. Nothing is thrown when everything succeeds; a single
failure is rethrown as it is, with its stack trace; several are thrown as one `AggregateException` with the operation failure first. Use
it where a set-up fails half way, so the cleanup failure does not hide the set-up failure:

```csharp
try
{
    dataSource = database.CreateDataSource();
    await MigrateSchema(dataSource, ct);
}
catch (Exception setupFailure)
{
    await TestCleanup.DisposeAll(setupFailure, dataSource, database);
    throw;
}
```

## Call proxy

`CallProxy.Create<IStore>(realStore, hooks => ...)` wraps a service so a test can hold, fail, follow up or replace a call without a
decorator class per service. The proxy is generated at compile time by the source generator that ships in this package, for every
interface named in `CallProxy.Create<T>` or `WrapWithCallProxy<T>` or hooked through `CallHooks<T>`. Each method of the interface gets a
hooks member of the same name with typed arguments and result, so no method name is a string, and a hook that does not fit the method
does not compile:

```csharp
var store = CallProxy.Create<IOrderStore>(real, hooks =>
{
    hooks.Find.Before(call => gate.Wait(call.Id));      // hold until released, or throw to fail
    hooks.Find.After(call => DeleteRow(call.Result));   // follow up with the typed result
    hooks.EmailExists.Returns(false);                   // replace the result, real service not called
});
```

- `Before` and `After` take an `Action<TCall>` or a `Func<TCall, Task>`. The call has a property per argument, the parameter name with an
  upper-case first letter (`call.Id` for `Guid id`, `call.Ct` for `CancellationToken ct`), and `Result` for a method that returns a value.
  Hooks of a method run in the order they were added. A before hook that throws stops the call; one that stays pending holds it. After
  hooks run once the call completed; an exception of the real service arrives unwrapped and skips them.
- `Returns(value)` exists only on a method with a result (`T`, `Task<T>` or `ValueTask<T>`) and takes a `T`; the before and after hooks
  still run, the real service does not. A later `Returns` replaces the value.
- Methods returning `Task`, `ValueTask`, `Task<T>` or `ValueTask<T>` are awaited. Every other method is synchronous: its hooks are waited
  for on the calling thread, so keep them short and never let them wait for the caller.
- Overloads get members named after their parameter types, such as `hooks.Save_Int32` and `hooks.Save_NoParameters`. The
  [generator README](../SharedKernel.Testing.SourceGenerator/README.md) lists every naming rule and the generated code.
- The interface may be public or internal, generic (closed, such as `IRepository<Order>`) and inherit other interfaces. Properties,
  indexers, events, generic methods, `ref`, `out` and `in` parameters, by-reference results and ref struct parameters have no typed hooks,
  so such an interface fails the build with `SKPRX003`; a class fails with `SKPRX001` and an inaccessible interface with `SKPRX002`.

Reference `PandaBear.SharedKernel.Testing` directly from the test project, since a package that depends on it does not pass its analyzers
on, and compile C# 14, the default for .NET 10 (`SKPRX004` otherwise). A generic helper that wraps its own type parameter is not seen by the
generator: `CallProxy.Create` then throws an `InvalidOperationException` that names the interface, unless the interface is also named in
one of the forms above. No reflection or runtime code generation runs: the proxy is a plain generated class.

It fits a test that wraps a real implementation and needs one targeted hold, failure, follow-up or replaced result. It does not replace a
fake that implements the whole interface with recorded state.

### Wrapping a registered service

When the application resolves the service from dependency injection, `services.WrapWithCallProxy<IStore>(hooks => ...)` replaces its
registrations with proxies, so the test hooks the instance the application actually uses:

```csharp
services.WrapWithCallProxy<ICustomerStore>(hooks =>
{
    hooks.Find.After(call => DeleteRow(call.Result));
    hooks.EmailExists.Returns(false);
});
```

Call it after the application's own registrations. Every registration of the service without a key is wrapped in place and keeps its
lifetime (singleton, scoped or transient): the original moves under a private key, so the container still builds the real implementation
with its dependencies, in the same scope, and disposes it as before, while an instance registration stays undisposed. Keyed registrations
are left alone. `configure` runs once and the proxies share its hooks. A service without a registration to wrap throws
`InvalidOperationException`. The container must support keyed services, as `Microsoft.Extensions.DependencyInjection` does from .NET 8.

### Migrating from the string hooks

The string hooks (`hooks.Before(nameof(IStore.Find), ...)`, `CallInfo` with `Args` and an `object?` `Result`) are gone. Use the member of
the method instead, read typed properties instead of `call.Args[i]` and casts, and drop `return Task.CompletedTask` from hooks that do not
wait:

| Before | After |
| --- | --- |
| `hooks.Returns(nameof(IStore.EmailExists), false)` | `hooks.EmailExists.Returns(false)` |
| `hooks.Before(nameof(IStore.Find), call => Gate.Wait((Guid)call.Args[0]!))` | `hooks.Find.Before(call => Gate.Wait(call.Id))` |
| `hooks.After(nameof(IStore.Find), call => call.Result is null ? Task.CompletedTask : Delete((Guid)call.Args[0]!))` | `hooks.Find.After(call => call.Result is null ? Task.CompletedTask : Delete(call.Id))` |
| `Action<CallHooks>` | `Action<CallHooks<IStore>>` |

A hook shared by several interfaces, such as a `Func<CallInfo, Task>` that reads `call.Args[0]`, becomes a method that takes the values
it needs (`Delete(string table, Guid id)`) and a lambda per interface that passes them. A runtime `ArgumentException` or
`InvalidOperationException` for an unknown name, a `Returns` on a method without a result, or a replacement of the wrong type is now a
compile error.

## Repository files

`RepositoryRoot.Locate(marker)` walks up from the test's base directory to the nearest directory that holds `marker`, a file
name such as `MyRepository.slnx` or a relative path such as `src/Directory.Build.props`, and returns its full path. It throws
`DirectoryNotFoundException` naming the marker and the start directory when no directory holds it; `TryLocate` returns `null`
instead. Both take an optional start directory.

```csharp
var readme = File.ReadAllText(Path.Combine(RepositoryRoot.Locate("MyRepository.slnx"), "tools", "My.Tool", "README.md"));
```

`ReadmeCommandLines.Find(markdown, invocation)` returns the documented command lines of one invocation, such as `dotnet repo`:
the argument text of every trimmed line and inline code span that starts with the invocation and a space, without a trailing
`# comment` and after any leading `NAME=value` assignments. A bare `repo verify` in prose, `dotnet repository`, and the invocation
without arguments do not match. Pass `dotnet tool run repo --` to find the local-tool form. Split each result with the
command-line parser the tool uses, for example `CommandLineParser.SplitCommandLine` of System.CommandLine, and parse it against
the real command tree, so a renamed command or option fails the test.

## Process fakes

`ScriptedProcessRunner` is an `IProcessRunner` of `SharedKernel.Processes` that answers runs from a script instead of starting
processes, and records each run in `Runs` (`StartInfo`, `Forward`, `FileName`, and the `Arguments` as they were at the start):

| Factory | Answers |
| --- | --- |
| `Returning(output)` | every run with the same output |
| `Throwing(exception)` | every run by throwing synchronously, as a runner that cannot start the process does |
| `InOrder(outputs)` | the first run with the first output, and so on; a run beyond the script throws `InvalidOperationException` |
| `AnswersInOrder(answers)` | the first run with the first delegate of the start information and token, and so on |
| `ByFileName(outputs)` | by executable path; an executable without an output throws `KeyNotFoundException` |
| `Answering(answer)` | with what a delegate returns for the recorded run, for example by its first argument |
| `new ScriptedProcessRunner(respond)` | with an asynchronous delegate of the start information, the forward writer, and the token |

`ExecutableMap` is an `IExecutableResolver` over a dictionary: `new ExecutableMap()` finds nothing until `Set(command, path)`,
`new ExecutableMap(pairs)` finds the given commands, and `new ExecutableMap(fallback)` answers every command that is not set
with a delegate, such as `command => "/usr/bin/" + command`. `Set(command, null)` makes a command not found, fallback included.
Keep a test-local fake when it encodes a scenario, such as a runner that reads the configuration file a command was given.

```csharp
var runner = ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, "ripgrep v14.1.0:\n") });
var inventory = new PackageInventory(runner, new ExecutableMap(static command => "/fake/" + command));
// ... act ...
runner.Runs.Select(static run => run.FileName).ShouldBe(["/fake/cargo"]);
```

## Culture

`CultureScope` sets the current culture and UI culture (both to one culture, or each to its own) and restores the previous
ones on disposal; `CultureScope.Run(culture, action)` returns the result of a function run under one culture. The cultures flow
with the asynchronous context, so code awaited inside the scope sees them.
