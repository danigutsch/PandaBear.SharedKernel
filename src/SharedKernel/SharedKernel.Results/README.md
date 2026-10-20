# PandaBear.SharedKernel.Results

Transport-neutral result and option primitives make expected success/failure paths explicit. Use `Result` or `Result<T>` for operations, `Option<T>` for optional values, and the composition extensions for synchronous, task, and value-task flows. `ResultError`, `ResultStatus`, validation errors, and stable error codes carry failure details. The package bundles its error-catalog generator; do not also install the separate Results.SourceGenerator package.

## Install

Replace both placeholders with an actually validated candidate version and its private feed; no new post-namespace candidate is advertised yet.

```text
dotnet add package PandaBear.SharedKernel.Results --version <validated-candidate-version> --source <private-candidate-feed>
```

## Example

```csharp
using PandaBear.Results;

public static class Example
{
    public static Result<int> ParseCount(string text)
        => int.TryParse(text, out var value) && value >= 0
            ? Result.Ok(value)
            : Result.Invalid<int>("Count is invalid.", "count", "Use a non-negative integer.");
}
```

## Constraints

Check success/failure before accessing payloads; propagate cancellation in asynchronous flows. Keep business validation in its owning domain. Registry publication remains deferred.

Package ID: `PandaBear.SharedKernel.Results`. C# namespace: `PandaBear.Results`. Assembly/project identity: `SharedKernel.Results`.
