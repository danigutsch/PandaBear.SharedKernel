# SharedKernel.EventSourcing.Npgsql

Raw Npgsql adapter for the storage-neutral `SharedKernel.EventSourcing` abstractions.

This package owns reusable event-stream and projection-checkpoint persistence. Bounded contexts still
own composition, schema naming, migrations/initialization policy, and context-specific read models.
