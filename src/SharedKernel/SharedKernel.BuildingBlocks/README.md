# SharedKernel.BuildingBlocks

Reusable value-object and workflow building blocks shared by bounded contexts.

## Compensation

`Compensation.CompleteOrCompensate` runs a follow-up operation and invokes a caller-provided
compensation action if that operation fails. Use it for small best-effort cleanup around side
effects that cannot share a transaction.
