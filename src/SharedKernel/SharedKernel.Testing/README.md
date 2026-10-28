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
