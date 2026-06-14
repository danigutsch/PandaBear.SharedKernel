# SharedKernel.Functional

Core result and option primitives for shared-kernel workflows.

## Purpose

This project is the shared home for result-oriented primitives that model success, failure, and
 optional values across domain and application code.

It is intended to provide a dependency-light package surface that can stay reusable across bounded
 contexts and can later compose cleanly with other shared-kernel packages such as
 `SharedKernel.Mediator`.

## Current State

This project currently provides:

- `Option<T>` for optional non-null values
- `Result` and `Result<T>` for success and failure flows
- `ResultError` and `ResultStatus` for shared error details and status mapping
- composition helpers such as `Map`, `Bind`, `Match`, `TryGetValue`, `TryGetError`, and `ToResult`

## Usage Guidance

Use the functional primitives to model expected business and application outcomes explicitly.

### When to Use `Result` and `Result<T>`

- Use `Result<T>` for operations that return a value on success and can fail for expected reasons.
- Use `Result` for operations that only need to report success or failure.
- Prefer results for validation, conflict, not-found, and other business-visible failures.
- Do not use exceptions for normal domain or application control flow.

Example factory method:

```csharp
public static Result<Tour> Create(string identifier, string name)
{
    if (string.IsNullOrWhiteSpace(identifier))
    {
        return Result.Invalid<Tour>(
            "Tour identifier cannot be empty",
            "identifier",
            "Tour identifier cannot be empty");
    }

    return Result.Ok(new Tour(identifier, name));
}
```

Example update method:

```csharp
public Result UpdateSchedule(DateTime startDate, DateTime endDate)
{
    if (endDate <= startDate)
    {
        return Result.Invalid(
            "End date must be after start date",
            "schedule",
            "End date must be after start date");
    }

    StartDate = startDate;
    EndDate = endDate;

    return Result.Ok();
}
```

### When to Use `Option<T>`

- Use `Option<T>` for values that may be absent without that absence being an error.
- Prefer `Option<T>` over null-based APIs when the caller must handle presence explicitly.
- Typical cases include optional related entities, optional lookup results, and optional workflow inputs.

Example:

```csharp
public Task<Result<Option<Customer>>> FindByEmail(string email, CancellationToken ct);
```

### Usage Rules

- Check `IsSuccess` before reading `Value` from `Result<T>`.
- Read error information from `ErrorDetails`.
- Keep dedicated error factories close to the domain type or feature that owns the rule.
- Map results at the API boundary instead of leaking transport concerns into domain logic.

## Composition Model

The package keeps the core functional operators under the same method names for both synchronous and
 asynchronous composition:

- `Map`
- `Bind`
- `Match`

When asynchronous composition is needed, the package prefers overloads and lifted task-based extension
 methods over `Async`-suffixed names.

That means async composition is expressed with the same verbs over:

- synchronous delegates
- `Task`-returning delegates
- `ValueTask`-returning delegates
- `Task<Result<T>>` / `ValueTask<Result<T>>` receivers
- `Task<Option<T>>` / `ValueTask<Option<T>>` receivers

The package does not try to add async variants for every member. Async support is limited to the
 core composition operators so call sites can stay fluent without widening the surface unnecessarily.

Example:

```csharp
var summaryMessage = await FindTourSummaryAsync("VT-42")
    .Map(static summary => Task.FromResult(summary.ToUpperInvariant()))
    .Match(
        static summary => $"Tour summary: {summary}",
        static error => $"Lookup failed: {error.Detail}");
```

See `samples/Results/BasicResults.Sample` for a small end-to-end example that combines synchronous
 and asynchronous composition.

The package is intentionally small and dependency-light.
Future work can extend the composition surface and adoption across the rest of the repository.

## Algebraic Laws

The synchronous composition surface is expected to preserve the usual functional laws for the generic
 carriers:

- `Option<T>` obeys functor laws for `Map` and monad laws for `Bind`
- `Result<T>` obeys functor laws for successful values and monad laws for success-preserving composition
- failure states short-circuit `Bind` and preserve their status and error details through `Map`

The asynchronous overloads are intended to preserve the same semantics as the synchronous operators.
 They are tested as behavioral equivalents of the same `Map`, `Bind`, and `Match` contracts rather than
 as a separate algebra.

## Benchmark Notes

Benchmarks for this package live under `benchmarks/SharedKernel.Functional.Benchmarks`.

Current benchmark findings:

- the current `readonly struct` carrier shape is already efficient for normal `Result<T>` payloads
- changing `Result` or `Option` to `ref struct` is not justified by current measurements
- larger struct payloads do amplify copy-sensitive paths such as `TryGetValue`
- benchmark-only `in` experiments help some large-struct projection helpers, but not consistently enough
  to justify broad public API changes

The current recommendation is to keep the public `Result` and `Option` type shapes stable and use
targeted internal experiments only if a specific large-struct hot path proves to matter.

## AOT and Trimming

This package is marked `IsAotCompatible=true` so trim and Native AOT analyzers run against the
 public surface.

## Dependencies

None.

## See Also

- [SharedKernel.Mediator.Abstractions](../SharedKernel.Mediator.Abstractions/README.md)
- [SharedKernel.Mediator](../SharedKernel.Mediator/README.md)
