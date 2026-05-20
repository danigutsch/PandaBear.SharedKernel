# BasicResults.Sample

Single console sample for `SharedKernel.Functional`.

## Purpose

This sample demonstrates the smallest end-to-end flow currently covered by the package:

- creating a successful `Result<T>`
- returning validation and not-found failures
- projecting values with `Match`
- converting nullable values with `Option.FromNullable`
- composing async `Task` and `ValueTask` flows with the same `Map` and `Match` method names

## Run

```bash
dotnet run --project samples/Results/BasicResults.Sample/BasicResults.Sample.csproj
```

## Notes

The sample stays intentionally small while still showing both success and failure flows for
`Result<T>` and `Option<T>`.

It also demonstrates the package rule for asynchronous composition: keep `Map`, `Bind`, and
`Match` under the same names and lift them over `Task`/`ValueTask` instead of introducing
`Async`-suffixed operator names.
