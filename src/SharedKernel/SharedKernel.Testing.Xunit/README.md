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

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Xunit`. C# namespace: `PandaBear.Testing`. Assembly/project identity:
`SharedKernel.Testing.Xunit`. Source is public; registry publication remains deferred.
