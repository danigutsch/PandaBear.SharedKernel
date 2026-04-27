# SharedKernel.Mediator.GeneratorTests

Unit tests for `SharedKernel.Mediator.SourceGenerator`.

## Scope

- Discovery-model generation from mediator contracts
- Generated discovery-report output
- Discovery diagnostics for missing, multiple, invalid-signature, inaccessible, and duplicate
  registration cases
- Generated DI registration output
- Generated mediator shell output
- Generated dispatch behavior compared with the reference dispatcher
- Deterministic generator behavior across repeated runs

## Current Traits

- `TestScope=Unit`
- `TestComponent=SharedKernel.Mediator.SourceGenerator`
- `TestCapability=Discovery`
- `TestCapability=DependencyInjection`
- `TestCapability=Dispatch`

## Dependencies

- `SharedKernel.Mediator.Abstractions`
- `SharedKernel.Mediator.SourceGenerator`
- `Microsoft.CodeAnalysis.CSharp`

## See Also

- [tests/README.md](../README.md)
- [SharedKernel.Mediator.SourceGenerator](../../src/Mediator/SharedKernel.Mediator.SourceGenerator/README.md)
