# SharedKernel.Mediator.Analyzers

Analyzer project for `SharedKernel.Mediator`.

## Purpose

This Roslyn component reports declaration and usage diagnostics for the mediator features that are
already implemented in the base packages.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKMED003` | Warning | Handler does not expose a compatible public `Handle` method. |
| `SKMED004` | Warning | Handler public `Handle` method omits `CancellationToken ct`. |
| `SKMED005` | Warning | Handler public `Handle` method returns the wrong `ValueTask<TResponse>` shape. |
| `SKMED006` | Warning | Mediator `Send` or `Publish` call does not forward the available `CancellationToken`. |
| `SKMED007` | Warning | Async stream `Handle` iterators must annotate `CancellationToken ct` with `[EnumeratorCancellation]`. |
| `SKMED008` | Info | Non-iterator async stream `Handle` methods should not declare an ineffective `CancellationToken ct`. |
| `SKMED020` | Warning | Open generic pipeline behavior declares an invalid type-parameter count. |
| `SKMED500` | Info | Handler calls mediator send APIs directly while CQRS strict rules are enabled. |

## Configuration

Use `.editorconfig` to tune analyzer behavior:

```ini
dotnet_diagnostic.SKMED006.severity = warning
dotnet_diagnostic.SKMED007.severity = warning
dotnet_diagnostic.SKMED500.severity = suggestion

sharedkernel_mediator_cqrs_strict = true
sharedkernel_mediator_allow_handler_to_handler_calls = false
sharedkernel_mediator_enable_cancellation_analysis = true
```

## Intentional diagnostic samples

### `SKMED003` invalid handler signature

```csharp
public sealed record LookupTour(string Code) : IQuery<string>;

public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
{
    ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
        => ValueTask.FromResult(request.Code);
}
```

### `SKMED004` missing `CancellationToken ct`

```csharp
public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
{
    public ValueTask<string> Handle(LookupTour request)
        => ValueTask.FromResult(request.Code);
}
```

### `SKMED005` handler return type mismatch

```csharp
public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
{
    public Task<string> Handle(LookupTour request, CancellationToken ct)
        => Task.FromResult(request.Code);
}
```

### `SKMED006` missing cancellation forwarding

```csharp
public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
{
    public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        => await sender.Send(new SearchTour(request.Code), CancellationToken.None);
}
```

### `SKMED020` invalid pipeline generic arity

```csharp
public sealed class ValidationBehavior<TRequest> : IPipelineBehavior<TRequest, int>
    where TRequest : IRequest<int>
{
    public ValueTask<int> Handle(TRequest request, RequestHandlerContinuation<int> next, CancellationToken ct)
        => next();
}
```

### `SKMED007` missing `[EnumeratorCancellation]`

```csharp
public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
{
    public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
    {
        await Task.Yield();
        yield return request.Count.ToString();
    }
}
```

### `SKMED008` ineffective `CancellationToken` on non-iterator stream handler

```csharp
public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
{
    public IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
        => GetTours(request);

    private static async IAsyncEnumerable<string> GetTours(StreamTours request)
    {
        await Task.Yield();
        yield return request.Count.ToString();
    }
}
```

### `SKMED500` handler-to-handler send in strict mode

```csharp
public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
{
    public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        => await sender.Send(new SearchTour(request.Code), ct);
}
```

## AOT scope note

There is currently no dedicated AOT-only analyzer range in the base package scope. The runtime and
source generator already avoid runtime fallback dispatch, so the repository keeps AOT posture in the
implementation and package documentation instead of adding speculative analyzer IDs.

## Dependencies

- `Microsoft.CodeAnalysis.Analyzers`
- `Microsoft.CodeAnalysis.CSharp`

## Package boundary

This package owns mediator contract and generated-dispatch diagnostics. Keep rules tied to
`SharedKernel.Mediator` APIs and source-generator behavior. Do not duplicate repository-wide style
rules or test-only rules here.

## See Also

- [SharedKernel.Mediator.CodeFixes](../SharedKernel.Mediator.CodeFixes/README.md)
- [SharedKernel.Mediator.SourceGenerator](../SharedKernel.Mediator.SourceGenerator/README.md)
