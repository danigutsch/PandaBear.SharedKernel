# PandaBear.SharedKernel.Results

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Transport-neutral result and option primitives make expected success/failure paths explicit. Use `Result` or `Result<T>` for operations, `Option<T>` for optional values, and the composition extensions for synchronous, task, and value-task flows. `ResultError`, `ResultStatus`, validation errors, and stable error codes carry failure details. The package bundles its error-catalog generator, which has no standalone package.

## Install

Replace both placeholders with an actually validated candidate version and its private feed; the placeholders do not identify a published package version.

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

## Generic code over Result and Result<T>

`Result` and `Result<T>` implement `IResultLike<TSelf>`: status, `IsFailure`, `ErrorDetails`/`TryGetError`, plus static factories
(`FromError`, `Invalid`, `NotFound`, `Unauthorized`, `Forbidden`, `Conflict`, `Error`, `CriticalError`, `Unavailable`). Constrain generic code with
`where TResponse : struct, IResultLike<TResponse>` (no boxing) so an open-generic behavior can short-circuit with
`TResponse.Invalid("Validation failed", "name", "Name is required")` for both shapes.

`default(Result)` and `default(Result<T>)` are uninitialized: `Status` is `Unknown` and both `IsSuccess` and `IsFailure` are false, so do
not treat "not a failure" as success. `Result` and `Result<T>` are the intended implementers of the interface.

## Constraints

Check success/failure before accessing payloads; propagate cancellation in asynchronous flows. Keep business validation in its owning domain. Registry publication remains deferred.

Package ID: `PandaBear.SharedKernel.Results`. C# namespace: `PandaBear.Results`. Assembly/project identity: `SharedKernel.Results`.
