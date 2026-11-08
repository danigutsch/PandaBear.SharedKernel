# SharedKernel.Results.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Analyzer project for misuse of `PandaBear.Results.Result` and `Result<T>`.

## Purpose

`Result` and `Result<T>` carry a status and error details instead of throwing. This Roslyn component reports the two
ways that pattern silently breaks: a raw result handed to the framework as an HTTP response, and a failure result that
nobody looks at.

Reference this package from any project that uses `SharedKernel.Results`, mainly application and web projects. Both
rules are inert in a compilation that does not reference `SharedKernel.Results`.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKRES001` | Warning | A minimal API endpoint delegate should not return `Result` or `Result<T>` directly. |
| `SKRES002` | Warning | The `Result` or `Result<T>` returned by a call should not be discarded. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKRES001.severity = warning
dotnet_diagnostic.SKRES002.severity = warning
```

## SKRES001

Reports a delegate passed to `MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`, `Map` or `MapMethods`
(`Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions`, also on a `RouteGroupBuilder`) whose return type is
`Result`, `Result<T>`, or a `Task<>` or `ValueTask<>` of either. The framework would serialize the raw struct as JSON:
the status and the error details reach the client and the HTTP status is always 200. A lambda is reported at its
arrow; a method group or local function is reported at the delegate argument.

```csharp
app.MapPost("/tours", (CreateTour command) => service.Create(command)); // SKRES001
```

Not reported: delegates returning `IResult`, `Results<...>`, DTOs or other types, including delegates whose returned
results end in `.ToHttpResult()`.

Fix: add `.ToHttpResult()` to the returned results (it returns `IResult` with the right status and safe details) and
reference the `PandaBear.SharedKernel.Results.AspNet` package. There is no automatic fix because wrapping every return
expression changes the delegate return type in ways that are not safe in every shape.

## SKRES002

Reports an expression statement whose value is a `Result` or `Result<T>` (including `Task<>` and `ValueTask<>` of them)
and which is neither assigned, returned, passed on, nor discarded: `Save();`, `await SaveAsync();`, or
`await SaveAsync().ConfigureAwait(false);`. A failed save then looks like success. The message names the method.

```csharp
repository.Save(order);          // SKRES002
await repository.Save(order);    // SKRES002
```

Not reported: `_ = Save(order);` and `_ = await Save(order);` (the explicit discard is the opt-out), results that are
assigned, returned, used in a condition or passed as an argument, and expression-bodied members that return the value.

Limitations: only direct expression statements are inspected; a result dropped through a conditional access
(`a?.Save();`) or a discarded `Task<Result>` stored and never awaited is not reported.

Fix: check `IsSuccess`, return or combine the result, or write `_ =` when ignoring it is intended. There is no
automatic fix package yet.

## Package boundary

This package owns Result-pattern misuse diagnostics. The shape of methods that return results (`SKSTYLE009`,
`SKSTYLE010`) stays in `SharedKernel.Style.Analyzers`.

## See Also

- [SharedKernel.Results](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Results/README.md)
- [SharedKernel.Results.AspNet](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Results.AspNet/README.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Results.Analyzers`. C# namespace: `PandaBear.Results.Analyzers`. Assembly/project identity: `SharedKernel.Results.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
