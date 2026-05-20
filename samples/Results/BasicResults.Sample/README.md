# BasicResults.Sample

Single console sample for `SharedKernel.Results`.

## Purpose

This sample demonstrates the smallest end-to-end flow currently covered by the package:

- creating a successful `Result<T>`
- returning validation and not-found failures
- projecting values with `Match`
- converting nullable values with `Option.FromNullable`

## Run

```bash
dotnet run --project samples/Results/BasicResults.Sample/BasicResults.Sample.csproj
```

## Notes

The sample stays intentionally small while still showing both success and failure flows for
`Result<T>` and `Option<T>`.
