# Mediator.Sample

Single console sample for `SharedKernel.Mediator`.

## Purpose

This sample demonstrates the smallest end-to-end CQRS flow currently supported by
the package set:

- a query and handler
- a command with a response and handler
- a notification publish and handler
- a streamed query and handler
- generated `AppMediator` dispatch
- generated `AddSharedKernelMediator()` DI registration

## Run

```bash
dotnet run --project samples/Mediator/Mediator.Sample/Mediator.Sample.csproj
```

## Notes

The sample keeps the flow small on purpose while still covering unary request,
notification publish, and response-stream dispatch through generated mediator
code.
