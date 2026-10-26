# SharedKernel.Results.SourceGenerator

Incremental source generator for centralized `PandaBear.Results` error catalogs. It has no standalone package: it ships inside `PandaBear.SharedKernel.Results` as `analyzers/dotnet/cs/SharedKernel.Results.SourceGenerator.dll`, so a consumer references that package alone and receives the generator automatically.

## Behavior

The generator discovers static result-returning error factories and properties and emits a typed error catalog provider. The consumer owns stable identifiers and business error ownership; the generator supplies catalog binding rather than business validation rules. It reports no diagnostics, so it has no diagnostic ID prefix.

## Example

```csharp
using PandaBear.Results;

public static class ItemErrors
{
    public static Result Missing() => Result.NotFound("Item was not found.");
}
```

C# namespace: `PandaBear.Results.SourceGenerator`. Assembly and project identity: `SharedKernel.Results.SourceGenerator`.
