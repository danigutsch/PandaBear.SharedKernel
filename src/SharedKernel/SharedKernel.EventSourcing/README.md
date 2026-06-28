# SharedKernel.EventSourcing

Storage-neutral event-sourcing primitives for aggregates, append/load operations, stream versions, and projections.

This project intentionally contains abstractions only. Reusable provider implementations belong in
adapter packages such as `SharedKernel.EventSourcing.PostgreSQL`; bounded-context infrastructure owns
composition, schema names, migrations, read models, and context-specific projection rebuild policy.
