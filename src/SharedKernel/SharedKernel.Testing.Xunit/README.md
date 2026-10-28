# SharedKernel.Testing.Xunit

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

xUnit v3 trait attributes for the shared test taxonomy. `SharedKernel.Testing` stays free of an xUnit dependency.

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

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Xunit`. C# namespace: `PandaBear.Testing`. Assembly/project identity:
`SharedKernel.Testing.Xunit`. Source is public; registry publication remains deferred.
