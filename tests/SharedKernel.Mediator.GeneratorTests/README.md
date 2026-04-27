# SharedKernel.Mediator.GeneratorTests

Unit tests for `SharedKernel.Mediator.SourceGenerator`.

## Scope

- Discovery-model generation from mediator contracts
- Generated discovery-report output
- Generated DI registration output
- Generated DI accessibility and duplicate-registration diagnostics
- Deterministic generator behavior across repeated runs

## Current Traits

- `TestScope=Unit`
- `TestComponent=SharedKernel.Mediator.SourceGenerator`
- `TestCapability=Discovery`
- `TestCapability=DependencyInjection`

## Dependencies

- `SharedKernel.Mediator.Abstractions`
- `SharedKernel.Mediator.SourceGenerator`
- `Microsoft.CodeAnalysis.CSharp`

## See Also

- [tests/README.md](../README.md)
- [SharedKernel.Mediator.SourceGenerator](../../src/Mediator/SharedKernel.Mediator.SourceGenerator/README.md)
