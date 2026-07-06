# SharedKernel.Scheduling

Lightweight scheduling primitives for hosted .NET services.

## What it provides

- `PollingBackgroundService`: drains available work in batches, then waits for the next poll.
- OpenTelemetry source and meter named `SharedKernel.Scheduling`.
- Stable low-cardinality telemetry tags for service name, outcome, item count, and error type.

## What it does not provide

This package is not a durable scheduler, queue, or job store. Callers remain responsible for
persistence, locking, idempotency, retries, and business-specific state.

## Telemetry

The polling loop emits:

- Activity name: `poll`
- Duration metric: `scheduling.polling.cycle.duration`, unit `s`
- Batch counter: `scheduling.polling.batch`
- Item counter: `scheduling.polling.item`
- Failure counter: `scheduling.polling.failure`
