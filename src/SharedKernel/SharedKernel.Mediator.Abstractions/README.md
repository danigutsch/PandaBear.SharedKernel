# SharedKernel.Mediator.Abstractions

Core contracts for the `SharedKernel.Mediator` stack.

## Purpose

This project defines the public request, handler, notification, stream, pipeline, and mediator
abstractions that the runtime, generator, analyzers, and tests build on.

## Current Contents

- Request contracts: `IRequest<TResponse>`, `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>`
- Handler contracts: `IRequestHandler<,>`, command/query handler variants
- Notification and streaming contracts, including response-stream, stream command/query, and duplex
  stream command/query markers, plus notification ordering/dispatch metadata
- Pipeline contracts and ordering metadata, including dedicated stream pipeline behaviors for
  stream-producing requests
- Mediator-facing contracts: `ISender`, `IPublisher`, `IMediator`, including generated request and
  response-stream dispatch via `Send(...)`
- Stream semantics follow the command/query families used by unary requests:
    - `IStreamCommand<TResponse>` and `IStreamQuery<TResponse>` extend `IStreamRequest<TResponse>`
    - `IStreamCommand<TRequestItem, TResponse>` and `IStreamQuery<TRequestItem, TResponse>` extend
      `ICommand<TResponse>` and `IQuery<TResponse>` while carrying streamed input through `Items`
    - `IDuplexStreamCommand<TRequestItem, TResponse>` and
      `IDuplexStreamQuery<TRequestItem, TResponse>` extend the streamed-response command/query
      contracts and also carry streamed input through `Items`
- `Unit` and `MediatorModuleAttribute`

## AOT and Trimming

This package is marked `IsAotCompatible=true` so trim and Native AOT analyzers run against the
abstractions surface.
The compatibility story for mediator stays documentation-first; the repository does not keep a
separate mediator AOT sample project.

## Dependencies

None.

## See Also

- [SharedKernel.Mediator](../SharedKernel.Mediator/README.md)
- [SharedKernel.Mediator.SourceGenerator](../SharedKernel.Mediator.SourceGenerator/README.md)
