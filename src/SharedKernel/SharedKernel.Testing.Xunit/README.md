# SharedKernel.Testing.Xunit

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

xUnit v3 trait attributes for the shared test taxonomy, and a logger provider that writes to the test output.
`SharedKernel.Testing` stays free of an xUnit dependency.

## Test scope

`TestScopeAttribute` declares the seam a test exercises. It emits the existing `Scope=<value>` trait, so
`--filter-trait "Scope=unit"`, reports and CI filters behave exactly as with `[Trait("Scope", "unit")]`.

```csharp
using PandaBear.Testing;

[assembly: TestScope(TestScope.Unit)]

[TestScope(TestScope.Integration)]
public sealed class OrderStoreTests { }
```

Allowed on assemblies, classes and methods. Values come from the `TestScope` enum in `SharedKernel.Testing`:
`Unit`, `Behavior`, `Component`, `Contract`, `UiIntegration`, `Integration`, `System`, `Architecture`.

## Other traits

All attributes emit plain traits, so existing filters keep working. Only `Scope` is meant to be required.

| Attribute | Trait | Values |
| --- | --- | --- |
| `TestHostAttribute(TestHost)` | `Host` | `in-process`, `test-server`, `aspire`, `browser` |
| `TestFocusAttribute(TestFocus)` | `Focus` | `core-behavior`, `domain-rule`, `edge-case`, `regression`, `security`, `drift-guard`, `migration` |
| `TestPriorityAttribute(TestPriority)` | `Priority` | `smoke`, `critical` |
| `TestAreaAttribute(string)` | `Area` | repository-specific |
| `TestSurfaceAttribute(string)` | `Surface` | repository-specific |

Enum values are the kebab-case member names. `TestFocus` and `TestPriority` apply to classes and methods only;
the others also apply to assemblies. Define repository-specific `Area` and `Surface` values once as constants.

## Logging to the test output

`TestOutputLoggerProvider` is an `ILoggerProvider` that writes each entry, as `level: category[event id] message` and its
exception, to the output of the xUnit test it belongs to, never to the process console. The runner shows a test's output
only when the test fails, while `dotnet test` (.NET 11 SDK) streams what the test process writes to its console for every
test. An entry belongs to the test running where it is logged; an entry logged outside any test, such as on a server's
request threads or in background work, belongs to the test that created the provider while that test runs. Any other entry
goes to the xUnit diagnostic messages of its context, if it has one, which `--xunit-diagnostics on` shows.

```csharp
services.AddLogging(logging => logging.AddProvider(new TestOutputLoggerProvider()));
```

`WebApplicationTestHost` and `KestrelTestHost` (SharedKernel.Testing.AspNetCore) and `AspireTestApplication`
(SharedKernel.IntegrationTesting) use it in place of the console logger.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Xunit`. C# namespace: `PandaBear.Testing`. Assembly/project identity:
`SharedKernel.Testing.Xunit`. Source is public; registry publication remains deferred.
