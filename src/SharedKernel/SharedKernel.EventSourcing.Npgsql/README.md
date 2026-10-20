# SharedKernel.EventSourcing.Npgsql

Raw Npgsql adapter for the storage-neutral `SharedKernel.EventSourcing` abstractions.

This package owns reusable event-stream and projection-checkpoint persistence. Bounded contexts still
own composition, schema naming, migrations/initialization policy, and context-specific read models.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.EventSourcing.Npgsql`. C# namespace: `PandaBear.EventSourcing.Npgsql`. Assembly/project identity: `SharedKernel.EventSourcing.Npgsql`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
